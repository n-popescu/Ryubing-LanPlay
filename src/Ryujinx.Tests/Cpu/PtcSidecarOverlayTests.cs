using System;
using System.IO;
using System.Security.Cryptography;

using ARMeilleure.CodeGen;
using ARMeilleure.CodeGen.Linking;
using ARMeilleure.CodeGen.Unwinding;
using ARMeilleure.State;
using ARMeilleure.Translation;
using ARMeilleure.Translation.PTC;

using NUnit.Framework;

using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Cpu.Jit;
using Ryujinx.Memory;

namespace Ryujinx.Tests.Cpu
{
    /// <summary>
    /// Behavioral coverage for the PTC sidecar overlay loader. Each test seeds either a
    /// stock cache, a sidecar cache, or both, then drives a fresh <see cref="Ptc"/> instance
    /// through the overlay-aware <c>Initialize</c>/<c>LoadTranslations</c> path and asserts
    /// the spec's contract: sidecar wins on duplicates, modded NSO ranges are skipped from
    /// stock, hash mismatches reject entries, and stock files are never mutated.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal sealed class PtcSidecarOverlayTests
    {
        private const ulong PageSize = 0x1000;

        // Backing-store layout for the synthetic guest memory:
        //   VA 0x1000 -> backing offset 0      (one mapped page, "main")
        //   VA 0x2000 -> backing offset 0x1000 (one mapped page, "subsdk9")
        // The block is sized to PageSize * 4 to leave headroom for any future expansion.
        private const ulong MainAddress = PageSize;
        private const ulong Subsdk9Address = PageSize * 2;
        private const ulong MainPatchedAddress = MainAddress + 0x10;
        private const ulong MainUnpatchedAddress = MainAddress + 0x20;
        private const ulong MainUnpatchedRelocTarget = MainAddress + 0x30;
        private const ulong MainBackingOffset = 0;
        private const ulong Subsdk9BackingOffset = PageSize;

        // ARM64 RET opcode (0xD65F03C0) - a valid 4-byte instruction so guest-hash compute
        // produces a stable value that both write- and load-time hashing will agree on.
        private const uint RetOpcode = 0xD65F03C0;
        private const ulong InstructionSize = 4;

        private const string TitleIdText = "0100f2c0115b6000";
        private const string StockDisplayVersion = "1.1.2";

        // The suffix value itself is irrelevant to these tests - they only require that it
        // is well-formed (`-` + 16 hex chars, matching ExeFsPtcCacheKey.ComputeSuffix output)
        // and distinct from the stock display version. ExeFsPtcCacheKeyTests covers the
        // derivation; this fixture covers what the loader does with the resulting filename.
        private const string SyntheticSidecarSuffix = "-deadbeefcafebabe";
        private const string SidecarDisplayVersion = StockDisplayVersion + SyntheticSidecarSuffix;

        private string _baseDir;
        private MemoryBlock _ram;
#pragma warning disable NUnit1032 // disposed via DecrementReferenceCount in TearDown
        private MemoryManager _memory;
#pragma warning restore NUnit1032

        [SetUp]
        public void SetUp()
        {
            int pageBits = (int)ulong.Log2(PageSize);

            _baseDir = Path.Combine(Path.GetTempPath(), "Ryujinx.Tests", Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(_baseDir);
            AppDataManager.Initialize(_baseDir);

            _ram = new MemoryBlock(PageSize * 4);
            _memory = new MemoryManager(_ram, 1UL << (pageBits + 4));
            _memory.IncrementReferenceCount();
            _memory.Map(MainAddress, MainBackingOffset, PageSize, MemoryMapFlags.Private);
            _memory.Map(Subsdk9Address, Subsdk9BackingOffset, PageSize, MemoryMapFlags.Private);

            _memory.Write(MainAddress, RetOpcode);
            _memory.Write(MainPatchedAddress, RetOpcode);
            _memory.Write(MainUnpatchedAddress, RetOpcode);
            _memory.Write(MainUnpatchedRelocTarget, RetOpcode);
            _memory.Write(Subsdk9Address, RetOpcode);
        }

        [TearDown]
        public void TearDown()
        {
            _memory?.DecrementReferenceCount();
            _ram?.Dispose();

            // Best-effort cleanup. A leaked file handle (e.g., from a crashed test) shouldn't
            // mask the original failure with a TearDown exception.
            try
            {
                if (_baseDir != null && Directory.Exists(_baseDir))
                {
                    Directory.Delete(_baseDir, recursive: true);
                }
            }
            catch (IOException)
            {
                // Swallow - temp dirs are isolated per-fixture-run via Guid.
            }
            catch (UnauthorizedAccessException)
            {
                // Same rationale.
            }
        }

        [Test]
        public void StockEntryInUnchangedNsoIsImported()
        {
            _ = SeedStockCacheWithEntries(includeMain: true, includeSubsdk9: false);

            Translator translator = new(new JitMemoryAllocator(), _memory, for64Bits: true);
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(Subsdk9Address, PageSize)]);

            sidecarPtc.LoadTranslations(translator);

            Assert.That(translator.HasTranslatedFunction(MainAddress), Is.True);
        }

        [Test]
        public void StockEntryInModdedNsoRangeIsSkipped()
        {
            _ = SeedStockCacheWithEntries(includeMain: false, includeSubsdk9: true);

            Translator translator = new(new JitMemoryAllocator(), _memory, for64Bits: true);
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(Subsdk9Address, PageSize)]);

            sidecarPtc.LoadTranslations(translator);

            Assert.That(translator.HasTranslatedFunction(Subsdk9Address), Is.False);
        }

        [Test]
        public void StockEntryReferencingModdedNsoRangeIsSkipped()
        {
            using Ptc stockPtc = new();
            stockPtc.Initialize(TitleIdText, StockDisplayVersion, enabled: true, _memory.Type);
            stockPtc.WriteCompiledFunction(
                MainAddress,
                guestSize: InstructionSize,
                Ptc.ComputeHash(_memory, MainAddress, InstructionSize),
                highCq: false,
                CreateCompiledFunction(
                [
                    new RelocEntry(0, new Symbol(SymbolType.GuestAddress, Subsdk9Address)),
                ]));

            stockPtc.SaveForTests($"{stockPtc.CachePathActual}.cache");

            Translator translator = new(new JitMemoryAllocator(), _memory, for64Bits: true);
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(Subsdk9Address, PageSize)]);

            sidecarPtc.LoadTranslations(translator);

            Assert.That(translator.HasTranslatedFunction(MainAddress), Is.False);
        }

        [Test]
        public void StockEntryWithMismatchedHashIsRejected()
        {
            // Stock entry's stored Hash is computed over a different byte pattern than
            // what's in guest memory at load time - simulates a mod that shifts NSO bases
            // so a stale stock entry now points at unrelated bytes. Per-function hash
            // validation is the spec's correctness boundary; range filtering is just an
            // optimization. This test pins the correctness boundary.
            _ = SeedStockCacheWithEntries(
                includeMain: true,
                includeSubsdk9: false,
                mainHashOverride: new Hash128(0xDEAD_BEEFUL, 0xCAFE_BABEUL));

            Translator translator = new(new JitMemoryAllocator(), _memory, for64Bits: true);
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: []);

            sidecarPtc.LoadTranslations(translator);

            Assert.That(translator.HasTranslatedFunction(MainAddress), Is.False);
        }

        [Test]
        public void SidecarEntryWinsOverStockDuplicate()
        {
            // Both files contain an entry at MainAddress with valid hashes (memory bytes
            // unchanged between writes). Spec: sidecar entry must win, stock duplicate
            // must be skipped silently - not assert.
            _ = SeedStockCacheWithEntries(includeMain: true, includeSubsdk9: false, mainHighCq: false);
            SeedSidecarCacheWithMainEntry(highCq: true);

            Translator translator = new(new JitMemoryAllocator(), _memory, for64Bits: true);
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: []);

            using (Assert.EnterMultipleScope())
            {
                Assert.DoesNotThrow(() => sidecarPtc.LoadTranslations(translator));
                bool found = translator.Functions.TryGetValue(MainAddress, out TranslatedFunction func);
                Assert.That(found, Is.True);
                Assert.That(func?.HighCq, Is.True, "Sidecar (highCq=true) must win over stock (highCq=false).");
            }
        }

        [Test]
        public void FirstTimeModdedLaunchWithoutSidecarStillImportsStock()
        {
            // Sidecar file does not exist yet (first launch under this mod combo). Spec
            // requires stock gap-fill to run anyway.
            _ = SeedStockCacheWithEntries(includeMain: true, includeSubsdk9: false);

            Assume.That(File.Exists(GetSidecarCachePath()), Is.False, "Pre-condition: no sidecar yet.");

            Translator translator = new(new JitMemoryAllocator(), _memory, for64Bits: true);
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: []);

            sidecarPtc.LoadTranslations(translator);

            Assert.That(translator.HasTranslatedFunction(MainAddress), Is.True);
        }

        [Test]
        public void OverlayLoadLeavesStockCacheBytewiseUntouched()
        {
            string stockCachePath = SeedStockCacheWithEntries(includeMain: true, includeSubsdk9: true);
            byte[] before = HashFile(stockCachePath);

            Translator translator = new(new JitMemoryAllocator(), _memory, for64Bits: true);
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(Subsdk9Address, PageSize)]);

            sidecarPtc.LoadTranslations(translator);

            Assert.That(HashFile(stockCachePath), Is.EqualTo(before));
        }

        [Test]
        public void SidecarSaveWritesSuffixedFileAndLeavesStockUntouched()
        {
            // Spec line 85: "Stock cache files untouched by modded launches (no pollution,
            // no rewrites)." This test exercises the *write* path, complementing the load
            // path covered by OverlayLoadLeavesStockCacheBytewiseUntouched.
            string stockCachePath = SeedStockCacheWithEntries(includeMain: true, includeSubsdk9: false);
            byte[] before = HashFile(stockCachePath);

            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: []);
            sidecarPtc.WriteCompiledFunction(
                MainAddress,
                guestSize: InstructionSize,
                Ptc.ComputeHash(_memory, MainAddress, InstructionSize),
                highCq: false,
                CreateCompiledFunction());

            string sidecarCachePath = $"{sidecarPtc.CachePathActual}.cache";
            sidecarPtc.SaveForTests(sidecarCachePath);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(File.Exists(sidecarCachePath), Is.True);
                Assert.That(new FileInfo(sidecarCachePath).Length, Is.GreaterThan(0));
                Assert.That(Path.GetFileName(sidecarCachePath), Is.EqualTo($"{SidecarDisplayVersion}.cache"));
                Assert.That(HashFile(stockCachePath), Is.EqualTo(before));
            }
        }

        [Test]
        public void SidecarProfileImportsStockProfileOutsideModdedRange()
        {
            SeedStockProfileWithEntries();

            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(Subsdk9Address, PageSize)]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(sidecarPtc.Profiler.ProfiledFuncs.ContainsKey(MainAddress), Is.True);
                Assert.That(sidecarPtc.Profiler.ProfiledFuncs.ContainsKey(Subsdk9Address), Is.False);
            }
        }

        [Test]
        public void OverlayLaunchImportsSidecarProfileOutsideModdedRange()
        {
            SeedSidecarProfileWithEntries();

            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(Subsdk9Address, PageSize)]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(
                    sidecarPtc.Profiler.ProfiledFuncs.ContainsKey(MainAddress),
                    Is.True,
                    "sidecar .info entries outside modded ranges should be reused on overlay launches");
                Assert.That(
                    sidecarPtc.Profiler.ProfiledFuncs.ContainsKey(Subsdk9Address),
                    Is.False,
                    "sidecar .info entries inside modded ranges must not be replayed on overlay launches");
            }
        }

        [Test]
        public void OverlayLaunchStillSavesSidecarProfile()
        {
            SeedSidecarProfileWithEntries();

            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(Subsdk9Address, PageSize)]);
            sidecarPtc.Profiler.StaticCodeStart = MainAddress;
            sidecarPtc.Profiler.StaticCodeSize = Subsdk9Address + PageSize - MainAddress;
            sidecarPtc.Profiler.AddEntry(MainAddress, ExecutionMode.Aarch64, highCq: false);

            string sidecarProfilePath = $"{sidecarPtc.CachePathActual}.info";
            sidecarPtc.Profiler.SaveForTests(sidecarProfilePath);

            using Ptc reloadedAsPlainPtc = new();
            reloadedAsPlainPtc.Initialize(TitleIdText, SidecarDisplayVersion, enabled: true, _memory.Type);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(File.Exists(sidecarProfilePath), Is.True);
                Assert.That(new FileInfo(sidecarProfilePath).Length, Is.GreaterThan(0));
                Assert.That(reloadedAsPlainPtc.Profiler.ProfiledFuncs.ContainsKey(MainAddress), Is.True);
                Assert.That(reloadedAsPlainPtc.Profiler.ProfiledFuncs.ContainsKey(Subsdk9Address), Is.False);
            }
        }

        [Test]
        public void StockLaunchStillLoadsItsOwnProfile()
        {
            // Sanity check: the sidecar-skip behavior must NOT affect stock launches. A stock
            // Ptc (no overlay) must still load its .info file as before.
            SeedStockProfileWithEntries();

            using Ptc stockPtc = new();
            stockPtc.Initialize(TitleIdText, StockDisplayVersion, enabled: true, _memory.Type);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(stockPtc.Profiler.ProfiledFuncs.ContainsKey(MainAddress), Is.True);
                Assert.That(stockPtc.Profiler.ProfiledFuncs.ContainsKey(Subsdk9Address), Is.True);
            }
        }

        [Test]
        public void OverlayInitializeWritesSidecarProfileMetadata()
        {
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(Subsdk9Address, PageSize)]);

            string metadataPath = $"{sidecarPtc.CachePathActual}.profilemeta";
            bool fileExists = File.Exists(metadataPath);
            bool loaded = PtcSidecarProfileMetadata.TryLoad(metadataPath, out PtcSidecarProfileMetadata metadata);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(fileExists, Is.True);
                Assert.That(loaded, Is.True);
                Assert.That(metadata?.StockDisplayVersion, Is.EqualTo(StockDisplayVersion));
                Assert.That(metadata?.SidecarDisplayVersion, Is.EqualTo(SidecarDisplayVersion));
                Assert.That(metadata?.ModdedRanges, Has.Count.EqualTo(1));
                Assert.That(metadata?.ModdedRanges[0].Start, Is.EqualTo(Subsdk9Address));
                Assert.That(metadata?.ModdedRanges[0].Size, Is.EqualTo(PageSize));
            }
        }

        [Test]
        public void OverlayInitializeWritesNarrowPatchRangeMetadata()
        {
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(MainPatchedAddress, InstructionSize)]);

            string metadataPath = $"{sidecarPtc.CachePathActual}.profilemeta";
            bool loaded = PtcSidecarProfileMetadata.TryLoad(metadataPath, out PtcSidecarProfileMetadata metadata);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(loaded, Is.True);
                Assert.That(metadata?.ModdedRanges, Has.Count.EqualTo(1));
                Assert.That(metadata?.ModdedRanges[0].Start, Is.EqualTo(MainPatchedAddress));
                Assert.That(metadata?.ModdedRanges[0].Size, Is.EqualTo(InstructionSize));
            }
        }

        [Test]
        public void StockLaunchMinesSidecarProfileOutsidePersistedModdedRanges()
        {
            SeedSidecarProfileWithEntries();

            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(Subsdk9Address, PageSize)]);
            Assume.That(File.Exists($"{sidecarPtc.CachePathActual}.profilemeta"), Is.True);

            using Ptc stockPtc = new();
            stockPtc.Initialize(
                TitleIdText,
                StockDisplayVersion,
                enabled: true,
                _memory.Type,
                enableStockProfileSidecarMining: true);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(stockPtc.Profiler.ProfiledFuncs.ContainsKey(MainAddress), Is.True);
                Assert.That(stockPtc.Profiler.ProfiledFuncs.ContainsKey(Subsdk9Address), Is.False);
            }
        }

        [Test]
        public void StockLaunchMinesSidecarProfileOutsideNarrowRangeInSameNso()
        {
            SeedSidecarProfileWithAddresses(MainAddress, MainPatchedAddress);

            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(MainPatchedAddress, InstructionSize)]);
            Assume.That(File.Exists($"{sidecarPtc.CachePathActual}.profilemeta"), Is.True);

            using Ptc stockPtc = new();
            stockPtc.Initialize(
                TitleIdText,
                StockDisplayVersion,
                enabled: true,
                _memory.Type,
                enableStockProfileSidecarMining: true);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(stockPtc.Profiler.ProfiledFuncs.ContainsKey(MainAddress), Is.True);
                Assert.That(stockPtc.Profiler.ProfiledFuncs.ContainsKey(MainPatchedAddress), Is.False);
            }
        }

        [Test]
        public void StockCacheImportRejectsRelocInsideNarrowRangeAndKeepsRelocOutside()
        {
            using Ptc stockPtc = new();
            stockPtc.Initialize(TitleIdText, StockDisplayVersion, enabled: true, _memory.Type);
            stockPtc.WriteCompiledFunction(
                MainAddress,
                guestSize: InstructionSize,
                Ptc.ComputeHash(_memory, MainAddress, InstructionSize),
                highCq: false,
                CreateCompiledFunction(
                [
                    new RelocEntry(0, new Symbol(SymbolType.GuestAddress, MainPatchedAddress)),
                ]));
            stockPtc.WriteCompiledFunction(
                MainUnpatchedAddress,
                guestSize: InstructionSize,
                Ptc.ComputeHash(_memory, MainUnpatchedAddress, InstructionSize),
                highCq: false,
                CreateCompiledFunction(
                [
                    new RelocEntry(0, new Symbol(SymbolType.GuestAddress, MainUnpatchedRelocTarget)),
                ]));
            stockPtc.SaveForTests($"{stockPtc.CachePathActual}.cache");

            Translator translator = new(new JitMemoryAllocator(), _memory, for64Bits: true);
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(MainPatchedAddress, InstructionSize)]);

            sidecarPtc.LoadTranslations(translator);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(translator.HasTranslatedFunction(MainAddress), Is.False);
                Assert.That(translator.HasTranslatedFunction(MainUnpatchedAddress), Is.True);
            }
        }

        [Test]
        public void IsAddressInModdedRangeReturnsFalseForAddressBeforeFirstRange()
        {
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(0x2000, 0x100), (0x4000, 0x100)]);

            Assert.That(sidecarPtc.IsAddressInModdedRangeForOverlay(0x1fff), Is.False);
        }

        [Test]
        public void IsAddressInModdedRangeReturnsFalseForAddressAfterLastRange()
        {
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(0x2000, 0x100), (0x4000, 0x100)]);

            Assert.That(sidecarPtc.IsAddressInModdedRangeForOverlay(0x4100), Is.False);
        }

        [Test]
        public void IsAddressInModdedRangeTreatsRangesAsHalfOpen()
        {
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(0x2000, 0x100)]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(sidecarPtc.IsAddressInModdedRangeForOverlay(0x2000), Is.True);
                Assert.That(sidecarPtc.IsAddressInModdedRangeForOverlay(0x20ff), Is.True);
                Assert.That(sidecarPtc.IsAddressInModdedRangeForOverlay(0x2100), Is.False);
            }
        }

        [Test]
        public void IsAddressInModdedRangeReturnsFalseForGapBetweenRanges()
        {
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(0x2000, 0x100), (0x4000, 0x100)]);

            Assert.That(sidecarPtc.IsAddressInModdedRangeForOverlay(0x3000), Is.False);
        }

        [Test]
        public void IsAddressInModdedRangeReturnsFalseForEmptyRanges()
        {
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: []);

            Assert.That(sidecarPtc.IsAddressInModdedRangeForOverlay(0x2000), Is.False);
        }

        [Test]
        public void IsAddressInModdedRangeHandlesSingleRange()
        {
            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: [(0x2000, 0x100)]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(sidecarPtc.IsAddressInModdedRangeForOverlay(0x1fff), Is.False);
                Assert.That(sidecarPtc.IsAddressInModdedRangeForOverlay(0x2000), Is.True);
                Assert.That(sidecarPtc.IsAddressInModdedRangeForOverlay(0x20ff), Is.True);
                Assert.That(sidecarPtc.IsAddressInModdedRangeForOverlay(0x2100), Is.False);
            }
        }

        [Test]
        public void StockLaunchDoesNotMineSidecarsWhenConfigDisabled()
        {
            SeedSidecarProfileWithEntries();

            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: []);
            Assume.That(File.Exists($"{sidecarPtc.CachePathActual}.profilemeta"), Is.True);

            using Ptc stockPtc = new();
            stockPtc.Initialize(TitleIdText, StockDisplayVersion, enabled: true, _memory.Type);

            Assert.That(stockPtc.Profiler.ProfiledFuncs, Is.Empty);
        }

        [Test]
        public void StockLaunchMiningUpgradesExistingLowCqProfile()
        {
            SeedStockProfileWithEntries();

            using Ptc sidecarSeed = new();
            sidecarSeed.Initialize(TitleIdText, SidecarDisplayVersion, enabled: true, _memory.Type);
            sidecarSeed.Profiler.StaticCodeStart = MainAddress;
            sidecarSeed.Profiler.StaticCodeSize = PageSize;
            sidecarSeed.Profiler.AddEntry(MainAddress, ExecutionMode.Aarch64, highCq: false);
            sidecarSeed.Profiler.UpdateEntry(MainAddress, ExecutionMode.Aarch64, highCq: true);
            sidecarSeed.Profiler.SaveForTests($"{sidecarSeed.CachePathActual}.info");

            using Ptc sidecarPtc = InitializeSidecar(moddedRanges: []);
            Assume.That(File.Exists($"{sidecarPtc.CachePathActual}.profilemeta"), Is.True);

            using Ptc stockPtc = new();
            stockPtc.Initialize(
                TitleIdText,
                StockDisplayVersion,
                enabled: true,
                _memory.Type,
                enableStockProfileSidecarMining: true);

            Assert.That(stockPtc.Profiler.ProfiledFuncs[MainAddress].HighCq, Is.True);
        }

        [Test]
        public void StockLaunchMiningSkipsMetadataForDifferentStockVersion()
        {
            // Seed the sidecar .info first so the metadata-pointed file actually exists,
            // then construct the metadata with a non-matching StockDisplayVersion. Avoids
            // depending on InitializeSidecar writing a metadata file we then overwrite.
            SeedSidecarProfileWithEntries();

            string metadataPath = GetActualMetadataPath(SidecarDisplayVersion);
            PtcSidecarProfileMetadata.Create(
                stockDisplayVersion: "9.9.9",
                sidecarDisplayVersion: SidecarDisplayVersion,
                moddedRanges: [])
                .Save(metadataPath);

            using Ptc stockPtc = new();
            stockPtc.Initialize(
                TitleIdText,
                StockDisplayVersion,
                enabled: true,
                _memory.Type,
                enableStockProfileSidecarMining: true);

            Assert.That(stockPtc.Profiler.ProfiledFuncs, Is.Empty);
        }

        [Test]
        public void StockLaunchMiningSkipsUnreadableMetadata()
        {
            SeedSidecarProfileWithEntries();

            string metadataPath = GetActualMetadataPath(SidecarDisplayVersion);
            File.WriteAllText(metadataPath, "not json");

            using Ptc stockPtc = new();
            stockPtc.Initialize(
                TitleIdText,
                StockDisplayVersion,
                enabled: true,
                _memory.Type,
                enableStockProfileSidecarMining: true);

            Assert.That(stockPtc.Profiler.ProfiledFuncs, Is.Empty);
        }

        [Test]
        public void StockLaunchMiningCombinesEntriesAcrossMultipleSidecars()
        {
            // Two distinct sidecar combos for the same stock version: each contributes a different
            // address. After mining, the stock profile should contain both addresses.
            const string SidecarASuffix = "-1111111111111111";
            const string SidecarBSuffix = "-2222222222222222";
            string sidecarADisplayVersion = StockDisplayVersion + SidecarASuffix;
            string sidecarBDisplayVersion = StockDisplayVersion + SidecarBSuffix;

            SeedSidecarProfileWith(sidecarADisplayVersion, address: MainAddress);
            SeedSidecarProfileWith(sidecarBDisplayVersion, address: Subsdk9Address);

            // Modded ranges empty for both sidecars in this test — we want both addresses to merge.
            PtcSidecarProfileMetadata.Create(StockDisplayVersion, sidecarADisplayVersion, [])
                .Save(GetActualMetadataPath(sidecarADisplayVersion));
            PtcSidecarProfileMetadata.Create(StockDisplayVersion, sidecarBDisplayVersion, [])
                .Save(GetActualMetadataPath(sidecarBDisplayVersion));

            using Ptc stockPtc = new();
            stockPtc.Initialize(
                TitleIdText,
                StockDisplayVersion,
                enabled: true,
                _memory.Type,
                enableStockProfileSidecarMining: true);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(stockPtc.Profiler.ProfiledFuncs.ContainsKey(MainAddress), Is.True);
                Assert.That(stockPtc.Profiler.ProfiledFuncs.ContainsKey(Subsdk9Address), Is.True);
            }
        }

        [Test]
        public void StockLaunchMiningDeletesOrphanMetadataWhenSidecarProfileMissing()
        {
            // A .profilemeta exists but its companion .info does not (user pruned cache, mod was
            // uninstalled, etc). Mining must skip it AND garbage-collect the orphan to prevent
            // accumulation across launches.
            string metadataPath = GetActualMetadataPath(SidecarDisplayVersion);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(metadataPath));
            PtcSidecarProfileMetadata.Create(StockDisplayVersion, SidecarDisplayVersion, [])
                .Save(metadataPath);

            string orphanedProfilePath = Path.Combine(Path.GetDirectoryName(metadataPath), $"{SidecarDisplayVersion}.info");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(File.Exists(orphanedProfilePath), Is.False, "Pre-condition: .info does not exist.");
                Assert.That(File.Exists(metadataPath), Is.True, "Pre-condition: .profilemeta exists.");
            }

            using Ptc stockPtc = new();
            stockPtc.Initialize(
                TitleIdText,
                StockDisplayVersion,
                enabled: true,
                _memory.Type,
                enableStockProfileSidecarMining: true);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(stockPtc.Profiler.ProfiledFuncs, Is.Empty);
                Assert.That(File.Exists(metadataPath), Is.False, "orphan metadata must be cleaned up");
            }
        }

        // ------------------------------------------------------------------------
        // Test helpers
        // ------------------------------------------------------------------------

        private Ptc InitializeSidecar((ulong Start, ulong Size)[] moddedRanges)
        {
            Ptc ptc = new();
            ptc.Initialize(
                TitleIdText,
                SidecarDisplayVersion,
                enabled: true,
                _memory.Type,
                stockDisplayVersion: StockDisplayVersion,
                moddedAddressRanges: moddedRanges);

            return ptc;
        }

        private string SeedStockCacheWithEntries(
            bool includeMain,
            bool includeSubsdk9,
            bool mainHighCq = false,
            Hash128? mainHashOverride = null)
        {
            using Ptc stockPtc = new();
            stockPtc.Initialize(TitleIdText, StockDisplayVersion, enabled: true, _memory.Type);

            if (includeMain)
            {
                stockPtc.WriteCompiledFunction(
                    MainAddress,
                    guestSize: InstructionSize,
                    mainHashOverride ?? Ptc.ComputeHash(_memory, MainAddress, InstructionSize),
                    highCq: mainHighCq,
                    CreateCompiledFunction());
            }

            if (includeSubsdk9)
            {
                stockPtc.WriteCompiledFunction(
                    Subsdk9Address,
                    guestSize: InstructionSize,
                    Ptc.ComputeHash(_memory, Subsdk9Address, InstructionSize),
                    highCq: false,
                    CreateCompiledFunction());
            }

            string stockCachePath = $"{stockPtc.CachePathActual}.cache";
            stockPtc.SaveForTests(stockCachePath);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(File.Exists(stockCachePath), Is.True, "Stock cache seed failed to produce a file.");
                Assert.That(new FileInfo(stockCachePath).Length, Is.GreaterThan(0));
            }

            return stockCachePath;
        }

        private void SeedSidecarCacheWithMainEntry(bool highCq)
        {
            using Ptc sidecarSeed = new();
            sidecarSeed.Initialize(TitleIdText, SidecarDisplayVersion, enabled: true, _memory.Type);

            sidecarSeed.WriteCompiledFunction(
                MainAddress,
                guestSize: InstructionSize,
                Ptc.ComputeHash(_memory, MainAddress, InstructionSize),
                highCq: highCq,
                CreateCompiledFunction());

            string sidecarCachePath = $"{sidecarSeed.CachePathActual}.cache";
            sidecarSeed.SaveForTests(sidecarCachePath);

            Assert.That(File.Exists(sidecarCachePath), Is.True, "Sidecar cache seed failed.");
        }

        private void SeedStockProfileWithEntries()
        {
            using Ptc stockPtc = new();
            stockPtc.Initialize(TitleIdText, StockDisplayVersion, enabled: true, _memory.Type);
            stockPtc.Profiler.StaticCodeStart = MainAddress;
            stockPtc.Profiler.StaticCodeSize = Subsdk9Address + PageSize - MainAddress;

            stockPtc.Profiler.AddEntry(MainAddress, ExecutionMode.Aarch64, highCq: false);
            stockPtc.Profiler.AddEntry(Subsdk9Address, ExecutionMode.Aarch64, highCq: false);

            string stockProfilePath = $"{stockPtc.CachePathActual}.info";
            stockPtc.Profiler.SaveForTests(stockProfilePath);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(File.Exists(stockProfilePath), Is.True);
                Assert.That(new FileInfo(stockProfilePath).Length, Is.GreaterThan(0));
            }
        }

        private void SeedSidecarProfileWithEntries()
        {
            using Ptc sidecarSeed = new();
            sidecarSeed.Initialize(TitleIdText, SidecarDisplayVersion, enabled: true, _memory.Type);
            sidecarSeed.Profiler.StaticCodeStart = MainAddress;
            sidecarSeed.Profiler.StaticCodeSize = Subsdk9Address + PageSize - MainAddress;

            sidecarSeed.Profiler.AddEntry(MainAddress, ExecutionMode.Aarch64, highCq: false);
            sidecarSeed.Profiler.AddEntry(Subsdk9Address, ExecutionMode.Aarch64, highCq: false);

            string sidecarProfilePath = $"{sidecarSeed.CachePathActual}.info";
            sidecarSeed.Profiler.SaveForTests(sidecarProfilePath);

            Assert.That(File.Exists(sidecarProfilePath), Is.True, "Sidecar profile seed failed.");
        }

        private void SeedSidecarProfileWithAddresses(params ulong[] addresses)
        {
            using Ptc sidecarSeed = new();
            sidecarSeed.Initialize(TitleIdText, SidecarDisplayVersion, enabled: true, _memory.Type);
            sidecarSeed.Profiler.StaticCodeStart = MainAddress;
            sidecarSeed.Profiler.StaticCodeSize = Subsdk9Address + PageSize - MainAddress;

            foreach (ulong address in addresses)
            {
                sidecarSeed.Profiler.AddEntry(address, ExecutionMode.Aarch64, highCq: false);
            }

            string sidecarProfilePath = $"{sidecarSeed.CachePathActual}.info";
            sidecarSeed.Profiler.SaveForTests(sidecarProfilePath);

            Assert.That(File.Exists(sidecarProfilePath), Is.True, "Sidecar profile seed failed.");
        }

        private void SeedSidecarProfileWith(string sidecarDisplayVersion, ulong address)
        {
            using Ptc sidecarSeed = new();
            sidecarSeed.Initialize(TitleIdText, sidecarDisplayVersion, enabled: true, _memory.Type);
            sidecarSeed.Profiler.StaticCodeStart = MainAddress;
            sidecarSeed.Profiler.StaticCodeSize = Subsdk9Address + PageSize - MainAddress;

            sidecarSeed.Profiler.AddEntry(address, ExecutionMode.Aarch64, highCq: false);

            string sidecarProfilePath = $"{sidecarSeed.CachePathActual}.info";
            sidecarSeed.Profiler.SaveForTests(sidecarProfilePath);

            Assert.That(File.Exists(sidecarProfilePath), Is.True, "Sidecar profile seed failed.");
        }

        private static string GetActualMetadataPath(string sidecarDisplayVersion)
        {
            return Path.Combine(
                AppDataManager.GamesDirPath,
                TitleIdText,
                "cache",
                "cpu",
                "0",
                $"{sidecarDisplayVersion}.profilemeta");
        }

        private static string GetSidecarCachePath()
        {
            return Path.Combine(
                AppDataManager.GamesDirPath,
                TitleIdText,
                "cache",
                "cpu",
                "0",
                $"{SidecarDisplayVersion}.cache");
        }

        private static CompiledFunction CreateCompiledFunction(RelocEntry[] relocEntries = null)
        {
            return new CompiledFunction(new byte[8], new UnwindInfo([], 0), relocEntries == null ? RelocInfo.Empty : new RelocInfo(relocEntries));
        }

        private static byte[] HashFile(string path)
        {
            return SHA256.HashData(File.ReadAllBytes(path));
        }
    }
}
