using System.Text.Json.Serialization;

namespace Ryujinx.Common.Configuration.Hid.Controller.Motion
{
    [JsonConverter(typeof(JsonMotionConfigControllerConverter))]
    public class MotionConfigController
    {
        public MotionInputBackendType MotionBackend { get; set; }

        /// <summary>
        /// Gyro Sensitivity
        /// </summary>
        public int Sensitivity { get; set; }

        /// <summary>
        /// Gyro Deadzone
        /// </summary>
        public double GyroDeadzone { get; set; }

        /// <summary>
        /// Enable Motion Controls
        /// </summary>
        public bool EnableMotion { get; set; }

        /// <summary>
        /// Gyro Rotation in degrees (0, 90, 180, 270)
        /// </summary>
        public int GyroRotation { get; set; } = 0;
    }
}
