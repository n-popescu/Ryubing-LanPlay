using System;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Ryujinx.Input.Motion
{
    public class GyroCalibrationEntry
    {
        public string Name { get; set; } = string.Empty;
        public float BiasX { get; set; }
        public float BiasY { get; set; }
        public float BiasZ { get; set; }
        public DateTime CalibratedAt { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter<GyroCalibrationSource>))]
        public GyroCalibrationSource Source { get; set; }

        [JsonIgnore]
        public Vector3 Bias
        {
            get => new(BiasX, BiasY, BiasZ);
            set { BiasX = value.X; BiasY = value.Y; BiasZ = value.Z; }
        }
    }
}
