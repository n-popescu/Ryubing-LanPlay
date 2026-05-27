using System.Numerics;

namespace Ryujinx.Input.Motion
{
    public readonly struct GyroCalibrationResult
    {
        public bool Success { get; init; }
        public bool AbortedDueToMovement { get; init; }
        public Vector3 Bias { get; init; }

        /// <summary>
        /// Deadzone (in deg/s) inferred from the sample noise observed during this calibration.
        /// Sized to clear the sensor's residual noise after bias removal while preserving as much
        /// intentional slow motion as possible. Zero when no calibration succeeded.
        /// </summary>
        public float SuggestedDeadzone { get; init; }
    }
}
