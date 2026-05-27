using System;
using System.Numerics;

namespace Ryujinx.Input.Motion
{
    public class GyroCalibrator
    {
        // Rest detection: a controller is at rest when (a) the raw signal is below a hard cap that
        // any plausible bias would respect, (b) accel magnitude is near 1g (no acceleration), and
        // (c) the accel direction has not drifted since the rest window started (no rotation).
        // Using the raw gyro instead of the bias-corrected signal avoids the treadmill where a
        // learning bias makes the corrected signal small and locks in rest detection.
        private const float MaxRawGyroAtRest = 3.0f;            // °/s; rules out gameplay motion
        private const float RestAccelTolerance = 0.5f;          // m/s² magnitude deviation from gravity
        private const float Gravity = 9.81f;
        private const float MaxAccelOrientationDriftDeg = 2.0f; // accel direction allowed to wobble this much during rest
        private const ulong RestDurationMicros = 1_500_000;     // 1.5s of sustained rest required per window
        private const float EmaAlpha = 0.03f;                   // conservative learning rate
        private const float MaxBiasJumpPerWindow = 2.0f;        // °/s; reject windows that diverge too much from current Bias
        private const float ManualMovementThreshold = 5.0f;     // °/s; abort wizard if any sample exceeds this

        public Vector3 Bias { get; private set; } = Vector3.Zero;

        public bool PassiveLearningEnabled { get; set; } = true;

        private Vector3 _restAccumulator = Vector3.Zero;
        private int _restSampleCount;
        private ulong _restWindowStart;
        private bool _inRestWindow;
        private Vector3 _restAccelReference = Vector3.Zero;

        private bool _manualActive;
        private bool _manualStarted;
        private ulong _manualStart;
        private ulong _manualDurationMicros;
        private Vector3 _manualAccumulator;
        private Vector3 _manualSumSquares;
        private int _manualSampleCount;

        // Safety factor over the per-axis std-dev sum: ~4 covers ~99.99 % of Gaussian sensor noise.
        private const float SuggestedDeadzoneSigmaMultiplier = 4.0f;

        public event Action<Vector3> OnPassiveUpdate;
        public event Action<GyroCalibrationResult> ManualCalibrationCompleted;

        public bool IsManualCalibrationActive => _manualActive;

        public void SetBias(Vector3 bias)
        {
            Bias = bias;
        }

        public void BeginManualCalibration(ulong durationMicros)
        {
            _manualActive = true;
            _manualStarted = false;
            _manualStart = 0;
            _manualDurationMicros = durationMicros;
            _manualAccumulator = Vector3.Zero;
            _manualSumSquares = Vector3.Zero;
            _manualSampleCount = 0;
        }

        public void CancelManualCalibration()
        {
            if (!_manualActive)
            {
                return;
            }
            _manualActive = false;
            ManualCalibrationCompleted?.Invoke(new GyroCalibrationResult
            {
                Success = false,
                AbortedDueToMovement = false,
                Bias = Bias,
            });
        }

        public Vector3 Process(Vector3 rawGyro, Vector3 accel, ulong timestamp)
        {
            if (_manualActive)
            {
                ProcessManual(rawGyro, timestamp);
                return rawGyro;
            }

            return ProcessPassive(rawGyro, accel, timestamp);
        }

        private void ProcessManual(Vector3 rawGyro, ulong timestamp)
        {
            if (!_manualStarted)
            {
                _manualStart = timestamp;
                _manualStarted = true;
            }

            if (rawGyro.Length() > ManualMovementThreshold)
            {
                _manualActive = false;
                ManualCalibrationCompleted?.Invoke(new GyroCalibrationResult
                {
                    Success = false,
                    AbortedDueToMovement = true,
                    Bias = Bias,
                });
                return;
            }

            _manualAccumulator += rawGyro;
            _manualSumSquares += new Vector3(rawGyro.X * rawGyro.X, rawGyro.Y * rawGyro.Y, rawGyro.Z * rawGyro.Z);
            _manualSampleCount++;

            if (timestamp - _manualStart >= _manualDurationMicros && _manualSampleCount > 0)
            {
                Vector3 mean = _manualAccumulator / _manualSampleCount;
                Vector3 meanOfSquares = _manualSumSquares / _manualSampleCount;
                float varianceSum = MathF.Max(0f, meanOfSquares.X - mean.X * mean.X)
                                  + MathF.Max(0f, meanOfSquares.Y - mean.Y * mean.Y)
                                  + MathF.Max(0f, meanOfSquares.Z - mean.Z * mean.Z);
                float suggestedDeadzone = MathF.Sqrt(varianceSum) * SuggestedDeadzoneSigmaMultiplier;

                Bias = mean;
                _manualActive = false;
                ManualCalibrationCompleted?.Invoke(new GyroCalibrationResult
                {
                    Success = true,
                    AbortedDueToMovement = false,
                    Bias = mean,
                    SuggestedDeadzone = suggestedDeadzone,
                });
            }
        }

        private Vector3 ProcessPassive(Vector3 rawGyro, Vector3 accel, ulong timestamp)
        {
            Vector3 corrected = rawGyro - Bias;

            bool gyroAtRest = rawGyro.Length() < MaxRawGyroAtRest;
            bool accelMagnitudeAtRest = MathF.Abs(accel.Length() - Gravity) < RestAccelTolerance;
            bool accelOrientationStable = !_inRestWindow || AccelOrientationDriftDeg(accel) < MaxAccelOrientationDriftDeg;

            if (gyroAtRest && accelMagnitudeAtRest && accelOrientationStable)
            {
                if (!_inRestWindow)
                {
                    _inRestWindow = true;
                    _restWindowStart = timestamp;
                    _restAccumulator = Vector3.Zero;
                    _restSampleCount = 0;
                    _restAccelReference = accel;
                }

                _restAccumulator += rawGyro;
                _restSampleCount++;

                if (timestamp - _restWindowStart >= RestDurationMicros && _restSampleCount > 0)
                {
                    Vector3 windowMean = _restAccumulator / _restSampleCount;

                    if (PassiveLearningEnabled && (windowMean - Bias).Length() <= MaxBiasJumpPerWindow)
                    {
                        Bias = Bias * (1f - EmaAlpha) + windowMean * EmaAlpha;
                        OnPassiveUpdate?.Invoke(Bias);
                    }

                    // Start a new window but KEEP the original accel reference so cumulative
                    // orientation drift across many windows still breaks rest detection.
                    _restWindowStart = timestamp;
                    _restAccumulator = Vector3.Zero;
                    _restSampleCount = 0;
                }
            }
            else
            {
                _inRestWindow = false;
                _restAccumulator = Vector3.Zero;
                _restSampleCount = 0;
            }

            return corrected;
        }

        private float AccelOrientationDriftDeg(Vector3 currentAccel)
        {
            float currentLen = currentAccel.Length();
            float refLen = _restAccelReference.Length();
            if (currentLen < 0.001f || refLen < 0.001f)
            {
                return 0f;
            }
            float cos = Vector3.Dot(currentAccel, _restAccelReference) / (currentLen * refLen);
            cos = Math.Clamp(cos, -1f, 1f);
            return MathF.Acos(cos) * (180f / MathF.PI);
        }
    }
}
