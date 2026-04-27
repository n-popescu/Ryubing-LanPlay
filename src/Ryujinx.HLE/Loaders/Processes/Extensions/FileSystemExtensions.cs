using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Loader;
using LibHac.Ns;
using LibHac.Tools.FsSystem;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Gpu;
using Ryujinx.HLE.Loaders.Executables;
using Ryujinx.Memory;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using static Ryujinx.HLE.HOS.ModLoader;

namespace Ryujinx.HLE.Loaders.Processes.Extensions
{
    static class FileSystemExtensions
    {
        public static MetaLoader GetNpdm(this IFileSystem fileSystem)
        {
            MetaLoader metaLoader = new();

            if (fileSystem == null || !fileSystem.FileExists(ProcessConst.MainNpdmPath))
            {
                Logger.Warning?.Print(LogClass.Loader, "NPDM file not found, using default values!");

                metaLoader.LoadDefault();
            }
            else
            {
                metaLoader.LoadFromFile(fileSystem);
            }

            return metaLoader;
        }

        private static byte[] ReadNpdmBytes(IFileSystem exeFs)
        {
            if (exeFs == null || !exeFs.FileExists(ProcessConst.MainNpdmPath))
            {
                return [];
            }

            using UniqueRef<IFile> npdmFile = new();
            exeFs.OpenFile(ref npdmFile.Ref, ProcessConst.MainNpdmPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
            npdmFile.Get.GetSize(out long fileSize).ThrowIfFailure();

            byte[] bytes = new byte[fileSize];
            npdmFile.Get.Read(out _, 0, bytes).ThrowIfFailure();
            return bytes;
        }

        public static ProcessResult Load(this IFileSystem exeFs, Switch device, BlitStruct<ApplicationControlProperty> nacpData, MetaLoader metaLoader, byte programIndex, bool isHomebrew = false)
        {
            ulong programId = metaLoader.ProgramId;

            // Replace the whole ExeFs partition by the modded one.
            bool partitionReplaced = device.Configuration.VirtualFileSystem.ModLoader.ReplaceExefsPartition(programId, ref exeFs);

            if (partitionReplaced)
            {
                metaLoader = null;
            }

            // Reload the MetaLoader in case of ExeFs partition replacement.
            metaLoader ??= exeFs.GetNpdm();

            NsoExecutable[] nsoExecutables = new NsoExecutable[ProcessConst.ExeFsPrefixes.Length];

            for (int i = 0; i < nsoExecutables.Length; i++)
            {
                string name = ProcessConst.ExeFsPrefixes[i];

                if (!exeFs.FileExists($"/{name}"))
                {
                    continue; // File doesn't exist, skip.
                }

                Logger.Info?.Print(LogClass.Loader, $"Loading {name}...");

                using UniqueRef<IFile> nsoFile = new();

                exeFs.OpenFile(ref nsoFile.Ref, $"/{name}".ToU8Span(), OpenMode.Read).ThrowIfFailure();

                nsoExecutables[i] = new NsoExecutable(nsoFile.Release().AsStorage(), name);
            }

            // ExeFs file replacements.
            ModLoadResult modLoadResult = device.Configuration.VirtualFileSystem.ModLoader.ApplyExefsMods(programId, nsoExecutables);

            // Take the Npdm from mods if present.
            if (modLoadResult.Npdm != null)
            {
                metaLoader = modLoadResult.Npdm;
            }

            // Collect the Nsos, ignoring ones that aren't used.
            List<NsoExecutable> compactedNsos = [];
            List<int> compactedOriginalSlots = [];

            for (int slot = 0; slot < ProcessConst.ExeFsPrefixes.Length; slot++)
            {
                NsoExecutable nso = nsoExecutables[slot];

                if (nso == null)
                {
                    continue;
                }

                compactedNsos.Add(nso);
                compactedOriginalSlots.Add(slot);
            }

            nsoExecutables = [.. compactedNsos];

            // Apply Nsos patches.
            PatchApplyResult patchApplyResult = device.Configuration.VirtualFileSystem.ModLoader.ApplyNsoPatches(programId, nsoExecutables);
            bool[] sidecarModdedFlags = BuildSidecarModdedFlags(compactedOriginalSlots, modLoadResult.Replaces, modLoadResult.Stubs, patchApplyResult.PatchedSlots, partitionReplaced);
            bool[] wholeSlotModdedFlags = BuildWholeSlotModdedFlags(compactedOriginalSlots, modLoadResult.Replaces, modLoadResult.Stubs, partitionReplaced);

            byte[] npdmBytes = modLoadResult.NpdmBytes ?? ReadNpdmBytes(exeFs);
            bool hasExeFsMods = partitionReplaced || modLoadResult.Modified || modLoadResult.Npdm != null || patchApplyResult.PatchedSlots.Data != 0;

            ExeFsPtcCacheKey.SlotInfo[] slotInfos = new ExeFsPtcCacheKey.SlotInfo[ProcessConst.ExeFsPrefixes.Length];

            for (int slot = 0; slot < ProcessConst.ExeFsPrefixes.Length; slot++)
            {
                NsoExecutable nso = null;

                for (int index = 0; index < compactedOriginalSlots.Count; index++)
                {
                    if (compactedOriginalSlots[index] == slot)
                    {
                        nso = nsoExecutables[index];
                        break;
                    }
                }

                bool stubbed = modLoadResult.Stubs[1 << slot] && !modLoadResult.Replaces[1 << slot];

                slotInfos[slot] = new ExeFsPtcCacheKey.SlotInfo(
                    ProcessConst.ExeFsPrefixes[slot],
                    nso != null,
                    stubbed,
                    nso?.Program ?? [],
                    nso?.TextOffset ?? 0,
                    nso?.TextSize ?? 0,
                    nso?.RoOffset ?? 0,
                    nso?.RoSize ?? 0,
                    nso?.DataOffset ?? 0,
                    nso?.DataSize ?? 0,
                    nso?.BssSize ?? 0);
            }

            string ptcCacheVariantSuffix = hasExeFsMods ? ExeFsPtcCacheKey.ComputeSuffix(slotInfos, npdmBytes) : string.Empty;

            bool enablePtc = device.System.EnablePtc;

            if (!string.IsNullOrEmpty(ptcCacheVariantSuffix))
            {
                string moddedSlotNames = string.Join(
                    ", ",
                    sidecarModdedFlags
                        .Select((modded, index) => (modded, index))
                        .Where(x => x.modded)
                        .Select(x => nsoExecutables[x.index].Name));

                Logger.Info?.Print(LogClass.Ptc, $"ExeFS-modified PTC sidecar active: hash={ptcCacheVariantSuffix[1..]}, modded slots={moddedSlotNames}");
            }

            string programName = "";

            if (!isHomebrew && programId > 0x010000000000FFFF)
            {
                programName = nacpData.Value.Title[(int)device.System.State.DesiredTitleLanguage].NameString.ToString();

                if (string.IsNullOrWhiteSpace(programName))
                {
                    foreach (ApplicationControlProperty.ApplicationTitle appTitle in nacpData.Value.Title)
                    {
                        if (appTitle.Name[0] != 0)
                            continue;

                        programName = appTitle.NameString.ToString();
                    }
                }
            }

            // Initialize GPU.
            GraphicsConfig.TitleId = programId.ToString("X16");
            device.Gpu.HostInitalized.Set();

            if (!MemoryBlock.SupportsFlags(MemoryAllocationFlags.ViewCompatible))
            {
                device.Configuration.MemoryManagerMode = MemoryManagerMode.SoftwarePageTable;
            }

            ProcessResult processResult = ProcessLoaderHelper.LoadNsos(
                device,
                device.System.KernelContext,
                metaLoader,
                nacpData,
                diskCacheEnabled: enablePtc,
                ptcCacheVariantSuffix: ptcCacheVariantSuffix,
                wholeSlotModdedFlags: wholeSlotModdedFlags,
                patchRanges: patchApplyResult.Ranges,
                allowCodeMemoryForJit: true,
                name: programName,
                programId: metaLoader.ProgramId,
                programIndex: programIndex,
                arguments: null,
                executables: nsoExecutables);

            // TODO: This should be stored using ProcessId instead.
            device.System.LibHacHorizonManager.ArpIReader.ApplicationId = new LibHac.ApplicationId(programId);

            return processResult;
        }

        internal static bool[] BuildSidecarModdedFlags(
            IReadOnlyList<int> compactedOriginalSlots,
            BitVector32 replaces,
            BitVector32 stubs,
            BitVector32 patchedCompacted,
            bool partitionReplaced)
        {
            bool[] sidecarModdedFlags = new bool[compactedOriginalSlots.Count];

            for (int compactedIndex = 0; compactedIndex < compactedOriginalSlots.Count; compactedIndex++)
            {
                int originalSlot = compactedOriginalSlots[compactedIndex];
                int originalMask = 1 << originalSlot;
                int compactedMask = 1 << compactedIndex;

                sidecarModdedFlags[compactedIndex] =
                    partitionReplaced ||
                    replaces[originalMask] ||
                    stubs[originalMask] ||
                    patchedCompacted[compactedMask];
            }

            return sidecarModdedFlags;
        }

        internal static bool[] BuildWholeSlotModdedFlags(
            IReadOnlyList<int> compactedOriginalSlots,
            BitVector32 replaces,
            BitVector32 stubs,
            bool partitionReplaced)
        {
            bool[] wholeSlotModdedFlags = new bool[compactedOriginalSlots.Count];

            for (int compactedIndex = 0; compactedIndex < compactedOriginalSlots.Count; compactedIndex++)
            {
                int originalSlot = compactedOriginalSlots[compactedIndex];
                int originalMask = 1 << originalSlot;

                wholeSlotModdedFlags[compactedIndex] =
                    partitionReplaced ||
                    replaces[originalMask] ||
                    stubs[originalMask];
            }

            return wholeSlotModdedFlags;
        }
    }
}
