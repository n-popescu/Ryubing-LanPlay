using NUnit.Framework;

using Ryujinx.HLE.Loaders.Processes;

namespace Ryujinx.Tests.HLE.Loaders.Processes
{
    /// <summary>
    /// Suffix-stability tests modeled after a representative launch shape:
    /// rtld + main + subsdk9 + sdk present, with an ExeFS mod that replaces
    /// /main.npdm and /subsdk9. All program/NPDM bytes are synthesized in-test.
    /// </summary>
    [TestFixture]
    internal sealed class ExeFsPtcCacheKeyTests
    {
        private const int StockNpdmSeed = 0x10;
        private const int ModdedNpdmSeed = 0x20;
        private const int StockSubsdk9Seed = 0x30;
        private const int ModdedSubsdk9Seed = 0x40;

        // Sized to be representative without being slow. Real subsdk9 is hundreds of KB;
        // the hash treats all bytes equally, so 4 KB is enough to cover the code path.
        private const int NpdmSize = 0x400;
        private const int NsoSize = 0x1000;

        [Test]
        public void StockSuffixIsStableAcrossCalls()
        {
            ExeFsPtcCacheKey.SlotInfo[] slots = BuildSlots(subsdk9Modded: false);
            byte[] npdm = CreatePatternedBytes(NpdmSize, StockNpdmSeed);

            string first = ExeFsPtcCacheKey.ComputeSuffix(slots, npdm);
            string second = ExeFsPtcCacheKey.ComputeSuffix(slots, npdm);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.StartWith("-"));
            Assert.That(first, Has.Length.EqualTo(17));
        }

        [Test]
        public void ModdedSuffixIsStableAcrossCalls()
        {
            ExeFsPtcCacheKey.SlotInfo[] slots = BuildSlots(subsdk9Modded: true);
            byte[] npdm = CreatePatternedBytes(NpdmSize, ModdedNpdmSeed);

            string first = ExeFsPtcCacheKey.ComputeSuffix(slots, npdm);
            string second = ExeFsPtcCacheKey.ComputeSuffix(slots, npdm);

            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void ModdedLaunchProducesDifferentSuffixThanStock()
        {
            string stockSuffix = ExeFsPtcCacheKey.ComputeSuffix(
                BuildSlots(subsdk9Modded: false),
                CreatePatternedBytes(NpdmSize, StockNpdmSeed));

            string moddedSuffix = ExeFsPtcCacheKey.ComputeSuffix(
                BuildSlots(subsdk9Modded: true),
                CreatePatternedBytes(NpdmSize, ModdedNpdmSeed));

            Assert.That(moddedSuffix, Is.Not.EqualTo(stockSuffix));
        }

        [Test]
        public void NpdmReplacementAloneChangesSuffix()
        {
            // Same NSOs as stock, only /main.npdm differs (per-file npdm replacement scenario).
            ExeFsPtcCacheKey.SlotInfo[] slots = BuildSlots(subsdk9Modded: false);

            string stockSuffix = ExeFsPtcCacheKey.ComputeSuffix(slots, CreatePatternedBytes(NpdmSize, StockNpdmSeed));
            string moddedSuffix = ExeFsPtcCacheKey.ComputeSuffix(slots, CreatePatternedBytes(NpdmSize, ModdedNpdmSeed));

            Assert.That(moddedSuffix, Is.Not.EqualTo(stockSuffix));
        }

        [Test]
        public void Subsdk9ReplacementAloneChangesSuffix()
        {
            // Same /main.npdm bytes, only subsdk9 differs (NSO-only replacement scenario).
            byte[] npdm = CreatePatternedBytes(NpdmSize, StockNpdmSeed);

            string stockSuffix = ExeFsPtcCacheKey.ComputeSuffix(BuildSlots(subsdk9Modded: false), npdm);
            string moddedSuffix = ExeFsPtcCacheKey.ComputeSuffix(BuildSlots(subsdk9Modded: true), npdm);

            Assert.That(moddedSuffix, Is.Not.EqualTo(stockSuffix));
        }

        [Test]
        public void IdenticalEffectiveBytesProduceSameSuffix()
        {
            // Two different mod folders yielding identical final bytes must dedupe to the same sidecar.
            ExeFsPtcCacheKey.SlotInfo[] slotsA = BuildSlots(subsdk9Modded: true);
            ExeFsPtcCacheKey.SlotInfo[] slotsB = BuildSlots(subsdk9Modded: true);
            byte[] npdmA = CreatePatternedBytes(NpdmSize, ModdedNpdmSeed);
            byte[] npdmB = CreatePatternedBytes(NpdmSize, ModdedNpdmSeed);

            string suffixA = ExeFsPtcCacheKey.ComputeSuffix(slotsA, npdmA);
            string suffixB = ExeFsPtcCacheKey.ComputeSuffix(slotsB, npdmB);

            Assert.That(suffixA, Is.EqualTo(suffixB));
        }

        [Test]
        public void SectionMetadataChangeChangesSuffixEvenWhenProgramBytesEqual()
        {
            // A header rewrite that keeps Program bytes identical but shifts text/ro layout
            // must still produce a different sidecar — runtime base/size derivation depends on it.
            byte[] program = CreatePatternedBytes(NsoSize, StockSubsdk9Seed);
            byte[] npdm = CreatePatternedBytes(NpdmSize, StockNpdmSeed);

            ExeFsPtcCacheKey.SlotInfo[] slotsA =
            [
                new("subsdk9", Present: true, Stubbed: false, program,
                    TextOffset: 0, TextSize: 0x800,
                    RoOffset: 0x800, RoSize: 0x400,
                    DataOffset: 0xC00, DataSize: 0x400,
                    BssSize: 0),
            ];

            ExeFsPtcCacheKey.SlotInfo[] slotsB =
            [
                new("subsdk9", Present: true, Stubbed: false, program,
                    TextOffset: 0, TextSize: 0x900, // shifted
                    RoOffset: 0x900, RoSize: 0x300,
                    DataOffset: 0xC00, DataSize: 0x400,
                    BssSize: 0),
            ];

            Assert.That(
                ExeFsPtcCacheKey.ComputeSuffix(slotsA, npdm),
                Is.Not.EqualTo(ExeFsPtcCacheKey.ComputeSuffix(slotsB, npdm)));
        }

        [Test]
        public void StubbedSlotIsDistinctFromAbsentSlot()
        {
            byte[] npdm = CreatePatternedBytes(NpdmSize, StockNpdmSeed);

            ExeFsPtcCacheKey.SlotInfo[] absent =
            [
                new("subsdk0", Present: false, Stubbed: false, [], 0, 0, 0, 0, 0, 0, 0),
            ];

            ExeFsPtcCacheKey.SlotInfo[] stubbed =
            [
                new("subsdk0", Present: false, Stubbed: true, [], 0, 0, 0, 0, 0, 0, 0),
            ];

            Assert.That(
                ExeFsPtcCacheKey.ComputeSuffix(absent, npdm),
                Is.Not.EqualTo(ExeFsPtcCacheKey.ComputeSuffix(stubbed, npdm)));
        }

        /// <summary>
        /// Build a representative slot table: rtld + main + subsdk9 + sdk present, others absent.
        /// When <paramref name="subsdk9Modded"/> is true, subsdk9 is filled from a different seed,
        /// modeling a mod that replaces the entire NSO.
        /// </summary>
        private static ExeFsPtcCacheKey.SlotInfo[] BuildSlots(bool subsdk9Modded)
        {
            ExeFsPtcCacheKey.SlotInfo[] slots = new ExeFsPtcCacheKey.SlotInfo[ProcessConst.ExeFsPrefixes.Length];

            for (int index = 0; index < slots.Length; index++)
            {
                string name = ProcessConst.ExeFsPrefixes[index];
                bool present = name is "rtld" or "main" or "subsdk9" or "sdk";

                byte[] program = present
                    ? CreatePatternedBytes(
                        NsoSize,
                        name == "subsdk9"
                            ? (subsdk9Modded ? ModdedSubsdk9Seed : StockSubsdk9Seed)
                            : 0x80 + index)
                    : [];

                slots[index] = new ExeFsPtcCacheKey.SlotInfo(
                    name,
                    Present: present,
                    Stubbed: false,
                    program,
                    TextOffset: 0,
                    TextSize: present ? 0x800u : 0,
                    RoOffset: 0x800,
                    RoSize: present ? 0x400u : 0,
                    DataOffset: 0xC00,
                    DataSize: present ? 0x400u : 0,
                    BssSize: 0);
            }

            return slots;
        }

        private static byte[] CreatePatternedBytes(int length, int seed)
        {
            byte[] bytes = new byte[length];

            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = (byte)((seed + index) & 0xff);
            }

            return bytes;
        }
    }
}
