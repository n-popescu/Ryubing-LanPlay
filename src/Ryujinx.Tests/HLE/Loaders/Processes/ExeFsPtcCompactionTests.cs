using System;
using System.Collections.Generic;
using System.Collections.Specialized;

using NUnit.Framework;

using Ryujinx.HLE.HOS;
using Ryujinx.HLE.Loaders.Processes;
using Ryujinx.HLE.Loaders.Processes.Extensions;

namespace Ryujinx.Tests.HLE.Loaders.Processes
{
    [TestFixture]
    internal sealed class ExeFsPtcCompactionTests
    {
        private static readonly bool[] _expectedTwoFalse = [false, false];
        private static readonly bool[] _expectedFalseTrue = [false, true];
        private static readonly bool[] _expectedThreeTrues = [true, true, true];
        private static readonly bool[] _expectedThreeFalses = [false, false, false];

        private static int Slot(string name)
        {
            return Array.IndexOf(ProcessConst.ExeFsPrefixes, name);
        }

        [Test]
        public void PatchOnCompactedSlotFlagsThatSlotOnlyForSidecar()
        {
            // rtld absent, main stubbed (dropped), subsdk0 unmodified, sdk patched.
            // Compacted: [subsdk0, sdk].
            BitVector32 stubs = new();
            stubs[1 << Slot("main")] = true;

            BitVector32 patchedCompacted = new();
            patchedCompacted[1 << 1] = true; // compacted index 1 = sdk

            bool[] flags = FileSystemExtensions.BuildSidecarModdedFlags(
                compactedOriginalSlots: [Slot("subsdk0"), Slot("sdk")],
                replaces: new BitVector32(),
                stubs,
                patchedCompacted,
                partitionReplaced: false);

            Assert.That(flags, Is.EqualTo(_expectedFalseTrue));
        }

        [Test]
        public void PatchOnCompactedSlotDoesNotFlagWholeSlot()
        {
            bool[] flags = FileSystemExtensions.BuildWholeSlotModdedFlags(
                compactedOriginalSlots: [Slot("subsdk0"), Slot("sdk")],
                replaces: new BitVector32(),
                stubs: new BitVector32(),
                partitionReplaced: false);

            Assert.That(flags, Is.EqualTo(_expectedTwoFalse));
        }

        [Test]
        public void ReplacesUsesOriginalSlotIndexNotCompactedIndexForWholeSlot()
        {
            // main stubbed (dropped). subsdk0 unmodified, sdk replaced.
            // If the helper indexed Replaces by compacted index, this would mark subsdk0
            // (compacted index 0) instead of sdk. Pins the original-vs-compacted invariant.
            BitVector32 replaces = new();
            replaces[1 << Slot("sdk")] = true;

            BitVector32 stubs = new();
            stubs[1 << Slot("main")] = true;

            bool[] flags = FileSystemExtensions.BuildWholeSlotModdedFlags(
                compactedOriginalSlots: [Slot("subsdk0"), Slot("sdk")],
                replaces,
                stubs,
                partitionReplaced: false);

            Assert.That(flags, Is.EqualTo(_expectedFalseTrue));
        }

        [Test]
        public void PartitionReplacedFlagsEveryCompactedSlot()
        {
            bool[] flags = FileSystemExtensions.BuildWholeSlotModdedFlags(
                compactedOriginalSlots: [Slot("rtld"), Slot("main"), Slot("sdk")],
                replaces: new BitVector32(),
                stubs: new BitVector32(),
                partitionReplaced: true);

            Assert.That(flags, Is.EqualTo(_expectedThreeTrues));
        }

        [Test]
        public void StubOrReplaceOnSlotFlagsWholeSlot()
        {
            // Pins the OR semantics: replaces alone, stubs alone, and both together each flag the slot.
            int[] compactedSlots = [Slot("main"), Slot("subsdk0")];

            BitVector32 replacesOnly = new();
            replacesOnly[1 << Slot("subsdk0")] = true;

            BitVector32 stubsOnly = new();
            stubsOnly[1 << Slot("subsdk0")] = true;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(
                    FileSystemExtensions.BuildWholeSlotModdedFlags(compactedSlots, replacesOnly, new BitVector32(), partitionReplaced: false),
                    Is.EqualTo(_expectedFalseTrue));
                Assert.That(
                    FileSystemExtensions.BuildWholeSlotModdedFlags(compactedSlots, new BitVector32(), stubsOnly, partitionReplaced: false),
                    Is.EqualTo(_expectedFalseTrue));
                Assert.That(
                    FileSystemExtensions.BuildWholeSlotModdedFlags(compactedSlots, replacesOnly, stubsOnly, partitionReplaced: false),
                    Is.EqualTo(_expectedFalseTrue));
            }
        }

        [Test]
        public void NoModsProducesAllFalse()
        {
            bool[] flags = FileSystemExtensions.BuildSidecarModdedFlags(
                compactedOriginalSlots: [Slot("main"), Slot("subsdk0"), Slot("sdk")],
                replaces: new BitVector32(),
                stubs: new BitVector32(),
                patchedCompacted: new BitVector32(),
                partitionReplaced: false);

            Assert.That(flags, Is.EqualTo(_expectedThreeFalses));
        }

        [Test]
        public void MergeRangesReturnsEmptyForEmptyInput()
        {
            Assert.That(ProcessLoaderHelper.MergeRanges([]), Is.Empty);
        }

        [Test]
        public void MergeRangesKeepsSingleRange()
        {
            Assert.That(ProcessLoaderHelper.MergeRanges([(0x1000, 0x20)]), Is.EqualTo([(0x1000UL, 0x20UL)]));
        }

        [Test]
        public void MergeRangesKeepsNonOverlappingRangesSorted()
        {
            Assert.That(
                ProcessLoaderHelper.MergeRanges([(0x3000, 0x10), (0x1000, 0x20)]),
                Is.EqualTo([(0x1000UL, 0x20UL), (0x3000UL, 0x10UL)]));
        }

        [Test]
        public void MergeRangesMergesAdjacentRanges()
        {
            Assert.That(
                ProcessLoaderHelper.MergeRanges([(0x1000, 0x10), (0x1010, 0x20)]),
                Is.EqualTo([(0x1000UL, 0x30UL)]));
        }

        [Test]
        public void MergeRangesMergesOverlappingRangesToOuterSpan()
        {
            Assert.That(
                ProcessLoaderHelper.MergeRanges([(0x1000, 0x20), (0x1010, 0x20)]),
                Is.EqualTo([(0x1000UL, 0x30UL)]));
        }

        [Test]
        public void MergeRangesFiltersZeroSizeRanges()
        {
            Assert.That(
                ProcessLoaderHelper.MergeRanges([(0x1000, 0), (0x2000, 0x20)]),
                Is.EqualTo([(0x2000UL, 0x20UL)]));
        }

        [Test]
        public void PatchRangesMapToGuestAddresses()
        {
            List<(ulong Start, ulong Size)> ranges = ProcessLoaderHelper.BuildModdedAddressRanges(
                nsoBase: [0x1000, 0x2000, 0x0d2da000],
                nsoSizes: [0x1000u, 0x1000u, 0x10000u],
                wholeSlotModdedFlags: null,
                patchRanges: [new ModLoader.PatchedRange(2, 0x5000, 0x10)]);

            Assert.That(ranges, Is.EqualTo([(0x0d2df000UL, 0x10UL)]));
        }

        [Test]
        public void PatchRangesPreserveGaps()
        {
            List<(ulong Start, ulong Size)> ranges = ProcessLoaderHelper.BuildModdedAddressRanges(
                nsoBase: [0x1000],
                nsoSizes: [0x1000u],
                wholeSlotModdedFlags: null,
                patchRanges: [new ModLoader.PatchedRange(0, 0x10, 0x10), new ModLoader.PatchedRange(0, 0x40, 0x10)]);

            Assert.That(ranges, Is.EqualTo([(0x1010UL, 0x10UL), (0x1040UL, 0x10UL)]));
        }

        [Test]
        public void PatchRangesMergeWhenAdjacent()
        {
            List<(ulong Start, ulong Size)> ranges = ProcessLoaderHelper.BuildModdedAddressRanges(
                nsoBase: [0x1000],
                nsoSizes: [0x1000u],
                wholeSlotModdedFlags: null,
                patchRanges: [new ModLoader.PatchedRange(0, 0x10, 0x10), new ModLoader.PatchedRange(0, 0x20, 0x20)]);

            Assert.That(ranges, Is.EqualTo([(0x1010UL, 0x30UL)]));
        }

        [Test]
        public void PatchRangesMergeWhenOverlapping()
        {
            List<(ulong Start, ulong Size)> ranges = ProcessLoaderHelper.BuildModdedAddressRanges(
                nsoBase: [0x1000],
                nsoSizes: [0x1000u],
                wholeSlotModdedFlags: null,
                patchRanges: [new ModLoader.PatchedRange(0, 0x10, 0x20), new ModLoader.PatchedRange(0, 0x20, 0x20)]);

            Assert.That(ranges, Is.EqualTo([(0x1010UL, 0x30UL)]));
        }

        [Test]
        public void WholeSlotRangeAndTouchingPatchRangeMerge()
        {
            List<(ulong Start, ulong Size)> ranges = ProcessLoaderHelper.BuildModdedAddressRanges(
                nsoBase: [0x1000, 0x2000],
                nsoSizes: [0x1000u, 0x1000u],
                wholeSlotModdedFlags: [true, false],
                patchRanges: [new ModLoader.PatchedRange(1, 0, 0x10)]);

            Assert.That(ranges, Is.EqualTo([(0x1000UL, 0x1010UL)]));
        }
    }
}
