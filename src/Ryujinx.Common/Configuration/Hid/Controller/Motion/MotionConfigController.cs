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
        /// Allow the gyro calibrator to refine the bias estimate automatically while the
        /// controller is at rest during gameplay. When disabled, only manual wizard calibration
        /// updates the bias.
        /// </summary>
        public bool EnablePassiveCalibration { get; set; } = false;
    }
}
