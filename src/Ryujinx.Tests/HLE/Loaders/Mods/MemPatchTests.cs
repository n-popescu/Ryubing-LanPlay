using System.Collections.Generic;

using NUnit.Framework;

using Ryujinx.HLE.Loaders.Mods;

namespace Ryujinx.Tests.HLE.Loaders.Mods
{
    [TestFixture]
    internal sealed class MemPatchTests
    {
        [Test]
        public void AppliedRangeSubtractsProtectedOffset()
        {
            MemPatch patch = new();
            byte[] memory = new byte[0x80];
            List<(int Offset, int Size)> ranges = [];

            patch.Add(0x150, [0xaa, 0xbb]);

            int count = patch.Patch(memory, protectedOffset: 0x100, ranges);

            {
                Assert.That(count, Is.EqualTo(1));
                Assert.That(ranges, Is.EqualTo(new[] { (0x50, 2) }));
                Assert.That(memory[0x50], Is.EqualTo(0xaa));
                Assert.That(memory[0x51], Is.EqualTo(0xbb));
            }
        }

        [Test]
        public void AppliedRangeUsesClippedSize()
        {
            MemPatch patch = new();
            byte[] memory = new byte[0x10];
            List<(int Offset, int Size)> ranges = [];

            patch.Add(0x0c, [1, 2, 3, 4, 5, 6, 7, 8]);

            int count = patch.Patch(memory, appliedRanges: ranges);

            {
                Assert.That(count, Is.EqualTo(1));
                Assert.That(ranges, Is.EqualTo(new[] { (0x0c, 4) }));
                Assert.That(memory[0x0c..], Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            }
        }

        [Test]
        public void PatchBelowProtectedOffsetIsSkipped()
        {
            MemPatch patch = new();
            byte[] memory = new byte[0x80];
            List<(int Offset, int Size)> ranges = [];

            patch.Add(0xff, [0xaa]);

            int count = patch.Patch(memory, protectedOffset: 0x100, ranges);

            {
                Assert.That(count, Is.Zero);
                Assert.That(ranges, Is.Empty);
                Assert.That(memory, Is.All.Zero);
            }
        }

        [Test]
        public void PatchAtOrPastAdjustedEndIsSkipped()
        {
            MemPatch patch = new();
            byte[] memory = new byte[0x20];
            List<(int Offset, int Size)> ranges = [];

            patch.Add(0x120, [0xaa]);

            int count = patch.Patch(memory, protectedOffset: 0x100, ranges);

            {
                Assert.That(count, Is.Zero);
                Assert.That(ranges, Is.Empty);
                Assert.That(memory, Is.All.Zero);
            }
        }

        [Test]
        public void ZeroSizeCopyEmitsNoRangeAndDoesNotCount()
        {
            MemPatch patch = new();
            byte[] memory = new byte[0x10];
            List<(int Offset, int Size)> ranges = [];

            patch.Add(0x4, []);

            int count = patch.Patch(memory, appliedRanges: ranges);

            {
                Assert.That(count, Is.Zero);
                Assert.That(ranges, Is.Empty);
                Assert.That(memory, Is.All.Zero);
            }
        }

        [Test]
        public void DuplicateOffsetKeepsLatestPatch()
        {
            MemPatch patch = new();
            byte[] memory = new byte[4];
            List<(int Offset, int Size)> ranges = [];

            patch.Add(1, [0xaa]);
            patch.Add(1, [0xbb, 0xcc]);

            int count = patch.Patch(memory, appliedRanges: ranges);

            {
                Assert.That(count, Is.EqualTo(1));
                Assert.That(ranges, Is.EqualTo(new[] { (1, 2) }));
                Assert.That(memory, Is.EqualTo(new byte[] { 0, 0xbb, 0xcc, 0 }));
            }
        }

        [Test]
        public void AppliedRangesMayBeNull()
        {
            MemPatch patch = new();
            byte[] memory = new byte[2];

            patch.Add(0, [0x11]);

            int count = patch.Patch(memory);

            {
                Assert.That(count, Is.EqualTo(1));
                Assert.That(memory[0], Is.EqualTo(0x11));
            }
        }
    }
}
