using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Input;
using Ryujinx.Input.Motion;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;

namespace Ryujinx.Ava.UI.ViewModels.Input
{
    public partial class GyroCalibrationViewModel : BaseModel, IDisposable
    {
        private const ulong DurationMicros = 5_000_000; // 5s

        private readonly IGamepad _gamepad;
        private readonly GyroCalibrator _calibrator;
        private readonly MotionInputId _sensor;
        private readonly Stopwatch _stopwatch = new();
        private Timer _pollTimer;
        private volatile bool _disposed;

        [ObservableProperty] private float _liveX;
        [ObservableProperty] private float _liveY;
        [ObservableProperty] private float _liveZ;
        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private float _progress;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _completed;
        [ObservableProperty] private bool _success;

        [ObservableProperty] private float _suggestedDeadzone;
        [ObservableProperty] private double _currentDeadzone;
        [ObservableProperty] private float _biasX;
        [ObservableProperty] private float _biasY;
        [ObservableProperty] private float _biasZ;

        public event Action Closed;
        public event Action<double> ApplyDeadzoneRequested;

        public GyroCalibrationViewModel(IGamepad gamepad, GyroCalibrator calibrator, MotionInputId sensor, double currentDeadzone)
        {
            _gamepad = gamepad;
            _calibrator = calibrator;
            _sensor = sensor;
            CurrentDeadzone = currentDeadzone;

            _calibrator.ManualCalibrationCompleted += OnCompleted;

            _pollTimer = new Timer(_ => Poll(), null, 0, 16);
        }

        private void Poll()
        {
            if (_disposed)
            {
                return;
            }

            Vector3 rawGyro = _gamepad.GetMotionData(MotionInputId.Gyroscope);
            Vector3 rawAccel = _gamepad.GetMotionData(MotionInputId.Accelerometer);

            // Match the axis permutation that NpadController applies to the live pipeline so the
            // bias is learned and stored in the same frame the live calibrator will subtract it in.
            Vector3 pipelineGyro = new(rawGyro.X, -rawGyro.Z, rawGyro.Y);
            Vector3 pipelineAccel = new(rawAccel.X, -rawAccel.Z, rawAccel.Y);

            Dispatcher.UIThread.Post(() =>
            {
                LiveX = pipelineGyro.X;
                LiveY = pipelineGyro.Y;
                LiveZ = pipelineGyro.Z;
                if (IsRunning)
                {
                    Progress = MathF.Min(1f, (float)(_stopwatch.Elapsed.TotalMilliseconds / (DurationMicros / 1000f)));
                }
            });

            if (IsRunning)
            {
                ulong ts = (ulong)(_stopwatch.Elapsed.TotalMilliseconds * 1000);
                _calibrator.Process(pipelineGyro, pipelineAccel, ts);
            }
        }

        public void Start()
        {
            IsRunning = true;
            Completed = false;
            Progress = 0f;
            StatusMessage = string.Empty;
            _stopwatch.Restart();
            _calibrator.BeginManualCalibration(DurationMicros);
        }

        public void Cancel()
        {
            if (IsRunning)
            {
                _calibrator.CancelManualCalibration();
            }
            Closed?.Invoke();
        }

        public void ApplyAndClose()
        {
            ApplyDeadzoneRequested?.Invoke(SuggestedDeadzone);
            Closed?.Invoke();
        }

        public void KeepAndClose()
        {
            Closed?.Invoke();
        }

        private void OnCompleted(GyroCalibrationResult result)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsRunning = false;
                Completed = true;
                Success = result.Success;
                if (result.Success)
                {
                    BiasX = result.Bias.X;
                    BiasY = result.Bias.Y;
                    BiasZ = result.Bias.Z;
                    SuggestedDeadzone = result.SuggestedDeadzone;
                    StatusMessage = LocaleManager.Instance[LocaleKeys.GyroCalibrationWindowSaved];
                    // Do not auto-close; the user reviews the suggestion and clicks Apply or Keep.
                }
                else if (result.AbortedDueToMovement)
                {
                    StatusMessage = LocaleManager.Instance[LocaleKeys.GyroCalibrationWindowMovementDetected];
                }
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pollTimer?.Dispose();
            _pollTimer = null;
            _calibrator.ManualCalibrationCompleted -= OnCompleted;
        }
    }
}
