using NUnit.Framework;
using Ryujinx.Input.Motion;
using System;
using System.Numerics;

namespace Ryujinx.Tests.Input
{
    [TestFixture]
    public class GyroCalibratorTests
    {
        // Helper: feed N samples at samplingRateHz with given gyro+accel.
        // Returns the corrected output of the last sample.
        private static Vector3 FeedSamples(GyroCalibrator calibrator, Vector3 gyro, Vector3 accel, int sampleCount, int samplingRateHz)
        {
            ulong dtMicros = (ulong)(1_000_000 / samplingRateHz);
            ulong timestamp = 0;
            Vector3 corrected = Vector3.Zero;
            for (int i = 0; i < sampleCount; i++)
            {
                timestamp += dtMicros;
                corrected = calibrator.Process(gyro, accel, timestamp);
            }
            return corrected;
        }

        [Test]
        public void Process_RawGyroAboveRestCap_ReturnsInputUnchanged()
        {
            var calibrator = new GyroCalibrator();
            Vector3 gyro = new(5.0f, 4.5f, -3.5f); // magnitude > MaxRawGyroAtRest (3.0)
            Vector3 accel = new(0f, 9.81f, 0f);

            Vector3 result = calibrator.Process(gyro, accel, 1000);

            Assert.That(result, Is.EqualTo(gyro));
            Assert.That(calibrator.Bias, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Process_RestWithBias_LearnsBiasOverTime()
        {
            var calibrator = new GyroCalibrator();
            Vector3 trueBias = new(0.4f, -0.2f, 0.1f);
            Vector3 accel = new(0f, 9.81f, 0f);

            // Window size is 1.5s. 3s of rest = 2 windows, enough to make some progress with alpha=0.03.
            FeedSamples(calibrator, trueBias, accel, 600, 200);
            Assert.That(calibrator.Bias.X, Is.GreaterThan(0.005f));
            Assert.That(calibrator.Bias.X, Is.LessThan(trueBias.X));

            // 150s of additional rest = 100 windows -> ~95% convergence with alpha=0.03.
            FeedSamples(calibrator, trueBias, accel, 30_000, 200);
            Assert.That(calibrator.Bias.X, Is.EqualTo(trueBias.X).Within(0.05f));
            Assert.That(calibrator.Bias.Y, Is.EqualTo(trueBias.Y).Within(0.05f));
            Assert.That(calibrator.Bias.Z, Is.EqualTo(trueBias.Z).Within(0.05f));
        }

        [Test]
        public void Process_SustainedSlowTilt_DoesNotLearnTheRotationAsBias()
        {
            var calibrator = new GyroCalibrator();
            // Physical scenario: controller is gently tilting at 3 deg/s around the Z axis.
            // The gyro Z reading reflects the actual angular velocity, and the accel gravity
            // direction tracks the rotation accordingly. Window is 1.5 s, so each window sees
            // 4.5 deg of orientation drift -- enough to break rest before any window completes.
            const float TiltDegPerSec = 3.0f;
            ulong dtMicros = 5000;
            ulong t = 0;
            float angleDeg = 0f;
            for (int i = 0; i < 4000; i++) // 20 s at 200 Hz
            {
                t += dtMicros;
                angleDeg += TiltDegPerSec / 200f;
                float rad = angleDeg * MathF.PI / 180f;
                Vector3 accel = new(MathF.Sin(rad) * 9.81f, MathF.Cos(rad) * 9.81f, 0f);
                Vector3 gyro = new(0f, 0f, TiltDegPerSec);
                calibrator.Process(gyro, accel, t);
            }

            // No window should complete during sustained rotation; bias must remain unchanged.
            Assert.That(calibrator.Bias.Length(), Is.LessThan(0.05f));
        }

        [Test]
        public void Process_AccelFarFrom1g_DoesNotLearn()
        {
            var calibrator = new GyroCalibrator();
            Vector3 trueBias = new(0.4f, 0f, 0f);
            Vector3 wrongAccel = new(0f, 20f, 0f);

            FeedSamples(calibrator, trueBias, wrongAccel, 2000, 200);

            Assert.That(calibrator.Bias, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Process_LargeBiasJump_IsRejected()
        {
            var calibrator = new GyroCalibrator();
            Vector3 accel = new(0f, 9.81f, 0f);

            FeedSamples(calibrator, new Vector3(0.1f, 0f, 0f), accel, 4000, 200);
            Vector3 biasBefore = calibrator.Bias;

            FeedSamples(calibrator, new Vector3(3.1f, 0f, 0f), accel, 200, 200);

            Assert.That(calibrator.Bias, Is.EqualTo(biasBefore));
        }

        [Test]
        public void ManualCalibration_StableInput_ProducesAccurateBias()
        {
            var calibrator = new GyroCalibrator();
            Vector3 trueBias = new(0.4f, -0.2f, 0.1f);
            Vector3 accel = new(0f, 9.81f, 0f);

            GyroCalibrationResult result = default;
            calibrator.ManualCalibrationCompleted += r => result = r;

            calibrator.BeginManualCalibration(durationMicros: 1_000_000);

            // 200 samples at 200Hz = 1s
            ulong t = 0;
            for (int i = 0; i < 200; i++)
            {
                t += 5000;
                calibrator.Process(trueBias, accel, t);
            }
            // Drive one more sample beyond the duration to trigger completion
            t += 5000;
            calibrator.Process(trueBias, accel, t);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Bias.X, Is.EqualTo(trueBias.X).Within(0.001f));
            Assert.That(calibrator.Bias.X, Is.EqualTo(trueBias.X).Within(0.001f));
            // Stable input -> near-zero variance -> near-zero suggested deadzone.
            Assert.That(result.SuggestedDeadzone, Is.LessThan(0.001f));
        }

        [Test]
        public void ManualCalibration_NoisyInput_SuggestsNonZeroDeadzone()
        {
            var calibrator = new GyroCalibrator();
            Vector3 trueBias = new(0.4f, -0.2f, 0.1f);
            Vector3 accel = new(0f, 9.81f, 0f);

            GyroCalibrationResult result = default;
            calibrator.ManualCalibrationCompleted += r => result = r;

            calibrator.BeginManualCalibration(durationMicros: 1_000_000);

            // Alternate samples above and below the bias by 0.1 deg/s per axis to inject deterministic noise.
            ulong t = 0;
            for (int i = 0; i < 200; i++)
            {
                t += 5000;
                float sign = (i % 2 == 0) ? 1f : -1f;
                Vector3 noisy = trueBias + new Vector3(0.1f * sign, 0.1f * sign, 0.1f * sign);
                calibrator.Process(noisy, accel, t);
            }
            t += 5000;
            calibrator.Process(trueBias, accel, t);

            // Per-axis std-dev is 0.1, so sqrt(3 * 0.01) * 4 ~ 0.69.
            Assert.That(result.SuggestedDeadzone, Is.EqualTo(0.69f).Within(0.05f));
            // Bias must still be recovered correctly despite the noise (symmetric noise averages out).
            Assert.That(result.Bias.X, Is.EqualTo(trueBias.X).Within(0.01f));
        }

        [Test]
        public void ManualCalibration_MovementDetected_Aborts()
        {
            var calibrator = new GyroCalibrator();
            Vector3 accel = new(0f, 9.81f, 0f);

            GyroCalibrationResult result = default;
            calibrator.ManualCalibrationCompleted += r => result = r;

            calibrator.BeginManualCalibration(durationMicros: 1_000_000);

            ulong t = 0;
            t += 5000;
            calibrator.Process(new Vector3(0.1f, 0f, 0f), accel, t);
            t += 5000;
            calibrator.Process(new Vector3(10f, 0f, 0f), accel, t); // movement!

            Assert.That(result.Success, Is.False);
            Assert.That(result.AbortedDueToMovement, Is.True);
        }

        [Test]
        public void ManualCalibration_ReturnsInputUnchangedDuringSampling()
        {
            var calibrator = new GyroCalibrator();
            calibrator.SetBias(new Vector3(0.5f, 0.5f, 0.5f));
            calibrator.BeginManualCalibration(durationMicros: 1_000_000);

            Vector3 raw = new(1f, 1f, 1f);
            Vector3 result = calibrator.Process(raw, new Vector3(0, 9.81f, 0), 5000);

            Assert.That(result, Is.EqualTo(raw), "must return raw values so wizard UI shows live raw gyro");
        }

        [Test]
        public void CancelManualCalibration_FiresEventWithFailure()
        {
            var calibrator = new GyroCalibrator();
            GyroCalibrationResult result = default;
            int callCount = 0;
            calibrator.ManualCalibrationCompleted += r => { result = r; callCount++; };

            calibrator.BeginManualCalibration(durationMicros: 1_000_000);
            calibrator.CancelManualCalibration();

            Assert.That(callCount, Is.EqualTo(1));
            Assert.That(result.Success, Is.False);
            Assert.That(result.AbortedDueToMovement, Is.False);
        }

        [Test]
        public void CancelManualCalibration_WhenNotActive_DoesNothing()
        {
            var calibrator = new GyroCalibrator();
            int callCount = 0;
            calibrator.ManualCalibrationCompleted += _ => callCount++;

            calibrator.CancelManualCalibration();

            Assert.That(callCount, Is.EqualTo(0));
        }

        [Test]
        public void Process_PassiveLearningDisabled_DoesNotUpdateBias()
        {
            var calibrator = new GyroCalibrator { PassiveLearningEnabled = false };
            Vector3 trueBias = new(0.4f, -0.2f, 0.1f);
            Vector3 accel = new(0f, 9.81f, 0f);

            FeedSamples(calibrator, trueBias, accel, 2000, 200);

            Assert.That(calibrator.Bias, Is.EqualTo(Vector3.Zero));
        }
    }
}
