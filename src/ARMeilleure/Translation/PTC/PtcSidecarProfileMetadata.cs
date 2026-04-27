using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ARMeilleure.Translation.PTC
{
    internal sealed class PtcSidecarProfileMetadata
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public string StockDisplayVersion { get; set; }
        public string SidecarDisplayVersion { get; set; }
        public List<AddressRange> ModdedRanges { get; set; } = [];

        public sealed class AddressRange
        {
            public ulong Start { get; set; }
            public ulong Size { get; set; }
        }

        public static PtcSidecarProfileMetadata Create(
            string stockDisplayVersion,
            string sidecarDisplayVersion,
            IReadOnlyList<(ulong Start, ulong Size)> moddedRanges)
        {
            PtcSidecarProfileMetadata metadata = new()
            {
                Version = CurrentVersion,
                StockDisplayVersion = stockDisplayVersion,
                SidecarDisplayVersion = sidecarDisplayVersion,
                ModdedRanges = [],
            };

            if (moddedRanges != null)
            {
                foreach ((ulong start, ulong size) in moddedRanges)
                {
                    metadata.ModdedRanges.Add(new AddressRange { Start = start, Size = size });
                }
            }

            return metadata;
        }

        public static bool TryLoad(string path, out PtcSidecarProfileMetadata metadata)
        {
            metadata = null;

            try
            {
                metadata = JsonSerializer.Deserialize<PtcSidecarProfileMetadata>(File.ReadAllText(path));
            }
            catch
            {
                return false;
            }

            return metadata != null &&
                   metadata.Version == CurrentVersion &&
                   !string.IsNullOrEmpty(metadata.StockDisplayVersion) &&
                   !string.IsNullOrEmpty(metadata.SidecarDisplayVersion) &&
                   metadata.ModdedRanges != null;
        }

        public void Save(string path)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(this));
        }
    }
}
