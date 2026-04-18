using System.Numerics;

namespace Ryujinx.Input
{
    /// <summary>
    /// Utility methods for motion data processing and transformations.
    /// </summary>
    public static class MotionUtils
    {
        /// <summary>
        /// Applies gyroscope rotation to motion data based on controller horizontal orientation.
        /// </summary>
        /// <param name="value">The motion vector to rotate</param>
        /// <param name="rotation">The rotation option (0, 1, 2, or 3)</param>
        /// <returns>The rotated motion vector</returns>
        public static Vector3 ApplyGyroRotation(Vector3 value, int rotation)
        {
            return rotation switch
            {
                90 => new Vector3(value.Z, value.X, -value.Y),
                180 => new Vector3(-value.X, value.Z, value.Y),
                270 => new Vector3(-value.Z, -value.X, value.Y),
                _ => value
            };
        }
    }
}

