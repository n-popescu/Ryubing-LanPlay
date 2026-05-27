using System.Text.Json.Serialization;

namespace Ryujinx.Input.Motion
{
    [JsonSerializable(typeof(GyroCalibrationFile))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    internal partial class GyroCalibrationStoreJsonContext : JsonSerializerContext { }
}
