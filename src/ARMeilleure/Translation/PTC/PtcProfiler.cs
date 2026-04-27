using ARMeilleure.State;
using Humanizer;
using Microsoft.IO;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using static ARMeilleure.Translation.PTC.PtcFormatter;
using Timer = System.Timers.Timer;

namespace ARMeilleure.Translation.PTC
{
    class PtcProfiler
    {
        private const string OuterHeaderMagicString = "Pohd\0\0\0\0";

        private const uint InternalVersion = 7010; //! Not to be incremented manually for each change to the ARMeilleure project.

        private static readonly uint[] _migrateInternalVersions =
        [
            1866,
            5518,
        ];

        private const int SaveInterval = 30; // Seconds.

        private const CompressionLevel SaveCompressionLevel = CompressionLevel.Fastest;

        private readonly Ptc _ptc;

        private readonly Timer _timer;

        private readonly ulong _outerHeaderMagic;

        private readonly ManualResetEvent _waitEvent;

        private readonly Lock _lock = new();

        private bool _disposed;

        private Hash128 _lastHash;

        public Dictionary<ulong, FuncProfile> ProfiledFuncs { get; private set; }

        public bool Enabled { get; private set; }

        public ulong StaticCodeStart { get; set; }
        public ulong StaticCodeSize { get; set; }

        public PtcProfiler(Ptc ptc)
        {
            _ptc = ptc;

            _timer = new Timer(SaveInterval.Seconds());
            _timer.Elapsed += TimerElapsed;

            _outerHeaderMagic = BinaryPrimitives.ReadUInt64LittleEndian(EncodingCache.UTF8NoBOM.GetBytes(OuterHeaderMagicString).AsSpan());

            _waitEvent = new ManualResetEvent(true);

            _disposed = false;

            ProfiledFuncs = new Dictionary<ulong, FuncProfile>();

            Enabled = false;
        }

        private void TimerElapsed(object _, ElapsedEventArgs __)
            => new Thread(PreSave) { Name = "Ptc.DiskWriter" }.Start();

        public void AddEntry(ulong address, ExecutionMode mode, bool highCq, bool blacklist = false)
        {
            if (IsAddressInStaticCodeRange(address))
            {
                Debug.Assert(!highCq);

                if (blacklist)
                {
                    lock (_lock)
                    {
                        ProfiledFuncs[address] = new FuncProfile(mode, highCq: false, true);
                    }
                }
                else
                {
                    lock (_lock)
                    {
                        ProfiledFuncs.TryAdd(address, new FuncProfile(mode, highCq: false, false));
                    }
                }
            }
        }

        public void UpdateEntry(ulong address, ExecutionMode mode, bool highCq, bool? blacklist = null)
        {
            if (IsAddressInStaticCodeRange(address))
            {
                Debug.Assert(highCq);

                lock (_lock)
                {
                    Debug.Assert(ProfiledFuncs.ContainsKey(address));

                    ProfiledFuncs[address] = new FuncProfile(mode, highCq: true, blacklist ?? ProfiledFuncs[address].Blacklist);
                }
            }
        }

        public bool IsAddressInStaticCodeRange(ulong address)
        {
            return address >= StaticCodeStart && address < StaticCodeStart + StaticCodeSize;
        }

        public ConcurrentQueue<(ulong address, FuncProfile funcProfile)> GetProfiledFuncsToTranslate(TranslatorCache<TranslatedFunction> funcs)
        {
            ConcurrentQueue<(ulong address, FuncProfile funcProfile)> profiledFuncsToTranslate = new();

            foreach (KeyValuePair<ulong, FuncProfile> profiledFunc in ProfiledFuncs)
            {
                if (!funcs.ContainsKey(profiledFunc.Key) && !profiledFunc.Value.Blacklist)
                {
                    profiledFuncsToTranslate.Enqueue((profiledFunc.Key, profiledFunc.Value));
                }
            }

            return profiledFuncsToTranslate;
        }

        public void ClearEntries()
        {
            ProfiledFuncs.Clear();
            ProfiledFuncs.TrimExcess();
        }

        public List<ulong> GetBlacklistedFunctions()
        {
            List<ulong> funcs = [];

            foreach ((ulong ptr, FuncProfile funcProfile) in ProfiledFuncs)
            {
                if (!funcProfile.Blacklist)
                    continue;

                if (!funcs.Contains(ptr))
                    funcs.Add(ptr);
            }

            return funcs;
        }

        public void PreLoad()
        {
            _lastHash = default;

            if (_ptc.HasOverlay)
            {
                string fileNameActual = $"{_ptc.CachePathActual}.info";
                string fileNameBackup = $"{_ptc.CachePathBackup}.info";

                if (File.Exists(fileNameActual) && new FileInfo(fileNameActual).Length != 0L)
                {
                    if (!TryImportOverlayProfile(fileNameActual) &&
                        File.Exists(fileNameBackup) && new FileInfo(fileNameBackup).Length != 0L)
                    {
                        _ = TryImportOverlayProfile(fileNameBackup);
                    }
                }
                else if (File.Exists(fileNameBackup) && new FileInfo(fileNameBackup).Length != 0L)
                {
                    _ = TryImportOverlayProfile(fileNameBackup);
                }

                ImportStockProfile();
            }
            else
            {
                string fileNameActual = $"{_ptc.CachePathActual}.info";
                string fileNameBackup = $"{_ptc.CachePathBackup}.info";

                FileInfo fileInfoActual = new(fileNameActual);
                FileInfo fileInfoBackup = new(fileNameBackup);

                if (fileInfoActual.Exists && fileInfoActual.Length != 0L)
                {
                    if (!Load(fileNameActual, false))
                    {
                        if (fileInfoBackup.Exists && fileInfoBackup.Length != 0L)
                        {
                            _ = Load(fileNameBackup, true);
                        }
                    }
                }
                else if (fileInfoBackup.Exists && fileInfoBackup.Length != 0L)
                {
                    _ = Load(fileNameBackup, true);
                }

                MineSidecarProfilesForStockLaunch();
            }
        }

        internal static (int Added, int Upgraded) MergeProfileHintsFromSidecar(
            Dictionary<ulong, FuncProfile> target,
            IReadOnlyDictionary<ulong, FuncProfile> source,
            IReadOnlyList<PtcSidecarProfileMetadata.AddressRange> moddedRanges)
        {
            int added = 0;
            int upgraded = 0;

            foreach ((ulong address, FuncProfile sourceProfile) in source)
            {
                if (IsInPersistedRange(address, moddedRanges))
                {
                    continue;
                }

                if (!target.TryGetValue(address, out FuncProfile targetProfile))
                {
                    target[address] = sourceProfile;
                    added++;
                    continue;
                }

                if (!targetProfile.HighCq && sourceProfile.HighCq)
                {
                    target[address] = new FuncProfile(targetProfile.Mode, highCq: true, targetProfile.Blacklist);
                    upgraded++;
                }
            }

            return (added, upgraded);
        }

        private static bool IsInPersistedRange(ulong address, IReadOnlyList<PtcSidecarProfileMetadata.AddressRange> ranges)
        {
            if (ranges == null)
            {
                return false;
            }

            foreach (PtcSidecarProfileMetadata.AddressRange range in ranges)
            {
                if (address >= range.Start && address < range.Start + range.Size)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<(string ProfilePath, PtcSidecarProfileMetadata Metadata)> EnumerateStockMiningCandidates()
        {
            string actualDirectory = Path.GetDirectoryName(_ptc.CachePathActual);
            string backupDirectory = Path.GetDirectoryName(_ptc.CachePathBackup);
            string prefix = $"{_ptc.DisplayVersion}-";

            HashSet<string> seenSidecars = [];

            foreach (string directory in new[] { actualDirectory, backupDirectory })
            {
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    continue;
                }

                foreach (string metadataPath in Directory.EnumerateFiles(directory, $"{prefix}*.profilemeta"))
                {
                    if (!PtcSidecarProfileMetadata.TryLoad(metadataPath, out PtcSidecarProfileMetadata metadata))
                    {
                        continue;
                    }

                    if (metadata.StockDisplayVersion != _ptc.DisplayVersion)
                    {
                        continue;
                    }

                    string profilePath = Path.Combine(directory, $"{metadata.SidecarDisplayVersion}.info");
                    if (!File.Exists(profilePath) || new FileInfo(profilePath).Length == 0L)
                    {
                        // Orphaned metadata: the sidecar .info it points at no longer exists.
                        // Garbage-collect the orphan so users who prune cache files manually don't
                        // accumulate stale .profilemeta indefinitely. Best-effort; ignore failures.
                        try
                        {
                            File.Delete(metadataPath);
                            Logger.Debug?.Print(LogClass.Ptc, $"Deleted orphaned sidecar metadata: {metadataPath}");
                        }
                        catch (IOException)
                        {
                        }
                        catch (UnauthorizedAccessException)
                        {
                        }

                        continue;
                    }

                    if (!seenSidecars.Add(metadata.SidecarDisplayVersion))
                    {
                        continue;
                    }

                    yield return (profilePath, metadata);
                }
            }
        }

        private void MineSidecarProfilesForStockLaunch()
        {
            if (_ptc.HasOverlay || !_ptc.EnableStockProfileSidecarMining)
            {
                return;
            }

            int addedTotal = 0;
            int upgradedTotal = 0;

            foreach ((string profilePath, PtcSidecarProfileMetadata metadata) in EnumerateStockMiningCandidates())
            {
                if (!TryLoadProfile(profilePath, invalidateOnFailure: false, out Dictionary<ulong, FuncProfile> sidecarProfile, out _))
                {
                    continue;
                }

                lock (_lock)
                {
                    (int added, int upgraded) = MergeProfileHintsFromSidecar(ProfiledFuncs, sidecarProfile, metadata.ModdedRanges);
                    addedTotal += added;
                    upgradedTotal += upgraded;
                }
            }

            if (addedTotal == 0 && upgradedTotal == 0)
            {
                return;
            }

            SaveStockProfileAfterMining();

            Logger.Info?.Print(
                LogClass.Ptc,
                $"Mined {addedTotal} new profile hints + {upgradedTotal} HighCq upgrades from ExeFS sidecar profiles.");
        }

        private void SaveStockProfileAfterMining()
        {
            // Best-effort write. A concurrent stock launch in another Ryujinx instance can hold
            // the file, in which case we fail-soft and skip this mining write — symmetric with
            // WriteSidecarProfileMetadata's IOException handling. The merged in-memory profile
            // is still used for this session; it just isn't persisted to disk.
            string fileNameActual = $"{_ptc.CachePathActual}.info";
            string fileNameBackup = $"{_ptc.CachePathBackup}.info";

            try
            {
                FileInfo fileInfoActual = new(fileNameActual);

                if (fileInfoActual.Exists && fileInfoActual.Length != 0L)
                {
                    File.Copy(fileNameActual, fileNameBackup, true);
                }

                Save(fileNameActual);
            }
            catch (IOException exception)
            {
                Logger.Warning?.Print(LogClass.Ptc, $"Stock profile mining write skipped: {exception.Message}");
            }
        }

        private void ImportStockProfile()
        {
            string fileNameActual = $"{_ptc.StockCachePathActual}.info";
            string fileNameBackup = $"{_ptc.StockCachePathBackup}.info";

            if (File.Exists(fileNameActual) && new FileInfo(fileNameActual).Length != 0L)
            {
                if (TryImportStockProfile(fileNameActual))
                {
                    return;
                }
            }

            if (File.Exists(fileNameBackup) && new FileInfo(fileNameBackup).Length != 0L)
            {
                _ = TryImportStockProfile(fileNameBackup);
            }
        }

        private bool TryImportStockProfile(string fileName)
        {
            if (!TryLoadProfile(fileName, invalidateOnFailure: false, out Dictionary<ulong, FuncProfile> stockProfile, out _))
            {
                return false;
            }

            lock (_lock)
            {
                foreach ((ulong address, FuncProfile profile) in stockProfile)
                {
                    if (!_ptc.IsAddressInModdedRangeForOverlay(address) && !ProfiledFuncs.ContainsKey(address))
                    {
                        ProfiledFuncs[address] = profile;
                    }
                }
            }

            return true;
        }

        private bool TryImportOverlayProfile(string fileName)
        {
            if (!TryLoadProfile(fileName, invalidateOnFailure: false, out Dictionary<ulong, FuncProfile> profile, out _))
            {
                return false;
            }

            lock (_lock)
            {
                foreach ((ulong address, FuncProfile funcProfile) in profile)
                {
                    if (!_ptc.IsAddressInModdedRangeForOverlay(address) && !ProfiledFuncs.ContainsKey(address))
                    {
                        ProfiledFuncs[address] = funcProfile;
                    }
                }
            }

            return true;
        }

        private bool Load(string fileName, bool isBackup)
        {
            if (!TryLoadProfile(fileName, invalidateOnFailure: true, out Dictionary<ulong, FuncProfile> profiledFuncs, out Hash128 lastHash))
            {
                return false;
            }

            ProfiledFuncs = profiledFuncs;
            _lastHash = lastHash;

            long fileSize = new FileInfo(fileName).Length;

            Logger.Info?.Print(LogClass.Ptc, $"{(isBackup ? "Loaded Backup Profiling Info" : "Loaded Profiling Info")} (size: {fileSize} bytes, profiled functions: {ProfiledFuncs.Count}).");

            return true;
        }

        private bool TryLoadProfile(string fileName, bool invalidateOnFailure, out Dictionary<ulong, FuncProfile> profiledFuncs, out Hash128 lastHash)
        {
            profiledFuncs = null;
            lastHash = default;

            using FileStream compressedStream = new(fileName, FileMode.Open);
            using DeflateStream deflateStream = new(compressedStream, CompressionMode.Decompress, true);
            void InvalidateIfNeeded()
            {
                if (invalidateOnFailure)
                {
                    InvalidateCompressedStream(compressedStream);
                }
            }

            OuterHeader outerHeader = DeserializeStructure<OuterHeader>(compressedStream);

            if (!outerHeader.IsHeaderValid())
            {
                InvalidateIfNeeded();

                return false;
            }

            if (outerHeader.Magic != _outerHeaderMagic)
            {
                InvalidateIfNeeded();

                return false;
            }

            if (outerHeader.InfoFileVersion != InternalVersion && !_migrateInternalVersions.Contains(outerHeader.InfoFileVersion))
            {
                InvalidateIfNeeded();

                return false;
            }

            if (outerHeader.Endianness != Ptc.GetEndianness())
            {
                InvalidateIfNeeded();

                return false;
            }

            using RecyclableMemoryStream stream = MemoryStreamManager.Shared.GetStream();
            Debug.Assert(stream.Seek(0L, SeekOrigin.Begin) == 0L && stream.Length == 0L);

            try
            {
                deflateStream.CopyTo(stream);
            }
            catch
            {
                InvalidateIfNeeded();

                return false;
            }

            Debug.Assert(stream.Position == stream.Length);

            _ = stream.Seek(0L, SeekOrigin.Begin);

            Hash128 expectedHash = DeserializeStructure<Hash128>(stream);

            Hash128 actualHash = Hash128.ComputeHash(GetReadOnlySpan(stream));

            if (actualHash != expectedHash)
            {
                InvalidateIfNeeded();

                return false;
            }

            switch (outerHeader.InfoFileVersion)
            {
                case InternalVersion:
                    profiledFuncs = Deserialize(stream);
                    break;
                case 1866:
                    profiledFuncs = Deserialize(stream, (address, profile) => (address + 0x500000UL, profile));
                    break;
                default:
                    Logger.Error?.Print(LogClass.Ptc, $"No migration path for {nameof(outerHeader.InfoFileVersion)} '{outerHeader.InfoFileVersion}'. Discarding cache.");
                    InvalidateIfNeeded();
                    return false;
            }

            Debug.Assert(stream.Position == stream.Length);

            lastHash = actualHash;

            return true;
        }

        private static Dictionary<ulong, FuncProfile> Deserialize(Stream stream, Func<ulong, FuncProfile, (ulong, FuncProfile)> migrateEntryFunc = null)
        {
            if (migrateEntryFunc != null)
            {
                return DeserializeAndUpdateDictionary(stream, DeserializeStructure<FuncProfile>, migrateEntryFunc);
            }

            return DeserializeDictionary<ulong, FuncProfile>(stream, DeserializeStructure<FuncProfile>);
        }

        private static Dictionary<ulong, FuncProfile> DeserializeAddBlacklist(Stream stream, Func<ulong, FuncProfile, (ulong, FuncProfile)> migrateEntryFunc = null)
        {
            if (migrateEntryFunc != null)
            {
                return DeserializeAndUpdateDictionary(stream, stream => { return new FuncProfile(DeserializeStructure<FuncProfilePreBlacklist>(stream)); }, migrateEntryFunc);
            }

            return DeserializeDictionary<ulong, FuncProfile>(stream, stream => { return new FuncProfile(DeserializeStructure<FuncProfilePreBlacklist>(stream)); });
        }

        private static ReadOnlySpan<byte> GetReadOnlySpan(MemoryStream memoryStream)
        {
            return new(memoryStream.GetBuffer(), (int)memoryStream.Position, (int)memoryStream.Length - (int)memoryStream.Position);
        }

        private static void InvalidateCompressedStream(FileStream compressedStream)
        {
            compressedStream.SetLength(0L);
        }

        private void PreSave()
        {
            _waitEvent.Reset();

            string fileNameActual = $"{_ptc.CachePathActual}.info";
            string fileNameBackup = $"{_ptc.CachePathBackup}.info";

            FileInfo fileInfoActual = new(fileNameActual);

            if (fileInfoActual.Exists && fileInfoActual.Length != 0L)
            {
                File.Copy(fileNameActual, fileNameBackup, true);
            }

            Save(fileNameActual);

            _waitEvent.Set();
        }

        private void Save(string fileName)
        {
            int profiledFuncsCount;

            OuterHeader outerHeader = new()
            {
                Magic = _outerHeaderMagic,

                InfoFileVersion = InternalVersion,
                Endianness = Ptc.GetEndianness(),
            };

            outerHeader.SetHeaderHash();

            using (MemoryStream stream = MemoryStreamManager.Shared.GetStream())
            {
                Debug.Assert(stream.Seek(0L, SeekOrigin.Begin) == 0L && stream.Length == 0L);

                stream.Seek(Unsafe.SizeOf<Hash128>(), SeekOrigin.Begin);

                lock (_lock)
                {
                    Serialize(stream, ProfiledFuncs);

                    profiledFuncsCount = ProfiledFuncs.Count;
                }

                Debug.Assert(stream.Position == stream.Length);

                stream.Seek(Unsafe.SizeOf<Hash128>(), SeekOrigin.Begin);
                Hash128 hash = Hash128.ComputeHash(GetReadOnlySpan(stream));

                stream.Seek(0L, SeekOrigin.Begin);
                SerializeStructure(stream, hash);

                if (hash == _lastHash)
                {
                    return;
                }

                using FileStream compressedStream = new(fileName, FileMode.OpenOrCreate);
                using DeflateStream deflateStream = new(compressedStream, SaveCompressionLevel, true);
                try
                {
                    SerializeStructure(compressedStream, outerHeader);

                    stream.WriteTo(deflateStream);

                    _lastHash = hash;
                }
                catch
                {
                    compressedStream.Position = 0L;

                    _lastHash = default;
                }

                if (compressedStream.Position < compressedStream.Length)
                {
                    compressedStream.SetLength(compressedStream.Position);
                }
            }

            long fileSize = new FileInfo(fileName).Length;

            if (fileSize != 0L)
            {
                Logger.Info?.Print(LogClass.Ptc, $"Saved Profiling Info (size: {fileSize} bytes, profiled functions: {profiledFuncsCount}).");
            }
        }

        internal void SaveForTests(string fileName)
        {
            Save(fileName);
        }

        private static void Serialize(Stream stream, Dictionary<ulong, FuncProfile> profiledFuncs)
        {
            SerializeDictionary(stream, profiledFuncs, SerializeStructure);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1/*, Size = 29*/)]
        private struct OuterHeader
        {
            public ulong Magic;

            public uint InfoFileVersion;

            public bool Endianness;

            public Hash128 HeaderHash;

            public void SetHeaderHash()
            {
                Span<OuterHeader> spanHeader = MemoryMarshal.CreateSpan(ref this, 1);

                HeaderHash = Hash128.ComputeHash(MemoryMarshal.AsBytes(spanHeader)[..(Unsafe.SizeOf<OuterHeader>() - Unsafe.SizeOf<Hash128>())]);
            }

            public bool IsHeaderValid()
            {
                Span<OuterHeader> spanHeader = MemoryMarshal.CreateSpan(ref this, 1);

                return Hash128.ComputeHash(MemoryMarshal.AsBytes(spanHeader)[..(Unsafe.SizeOf<OuterHeader>() - Unsafe.SizeOf<Hash128>())]) == HeaderHash;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1/*, Size = 6*/)]
        public struct FuncProfile
        {
            public ExecutionMode Mode;
            public bool HighCq;
            public bool Blacklist;

            public FuncProfile(ExecutionMode mode, bool highCq, bool blacklist)
            {
                Mode = mode;
                HighCq = highCq;
                Blacklist = blacklist;
            }

            public FuncProfile(FuncProfilePreBlacklist fp)
            {
                Mode = fp.Mode;
                HighCq = fp.HighCq;
                Blacklist = false;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1/*, Size = 5*/)]
        public struct FuncProfilePreBlacklist
        {
            public ExecutionMode Mode;
            public bool HighCq;

            public FuncProfilePreBlacklist(ExecutionMode mode, bool highCq)
            {
                Mode = mode;
                HighCq = highCq;
            }
        }

        public void Start()
        {
            if (_ptc.State is PtcState.Enabled or
                PtcState.Continuing)
            {
                Enabled = true;

                _timer.Enabled = true;
            }
        }

        public void Stop()
        {
            Enabled = false;

            if (!_disposed)
            {
                _timer.Enabled = false;
            }
        }

        public void Wait()
        {
            _waitEvent.WaitOne();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _timer.Elapsed -= TimerElapsed;
                _timer.Dispose();

                Wait();
                _waitEvent.Dispose();
            }
        }
    }
}
