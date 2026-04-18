using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Models.Input;
using Ryujinx.Ava.UI.ViewModels.Input;
using Ryujinx.Input;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Ryujinx.Ava.UI.Views.Input
{
    public partial class MotionInputView : RyujinxControl<MotionInputViewModel>
    {
        private DispatcherTimer _motionUpdateTimer;
        private ControllerInputViewModel _parentViewModel;

        public MotionInputView()
        {
            InitializeComponent();
        }

        public MotionInputView(ControllerInputViewModel viewModel)
        {
            _parentViewModel = viewModel;
            GamepadInputConfig config = viewModel.Config;

            ViewModel = new MotionInputViewModel
            {
                Slot = config.Slot,
                AltSlot = config.AltSlot,
                DsuServerHost = config.DsuServerHost,
                DsuServerPort = config.DsuServerPort,
                MirrorInput = config.MirrorInput,
                Sensitivity = config.Sensitivity,
                GyroDeadzone = config.GyroDeadzone,
                EnableCemuHookMotion = config.EnableCemuHookMotion,
                GyroRotation = config.GyroRotation,
            };

            InitializeComponent();
            
            Loaded += (_, _) => StartMotionUpdates();
            Unloaded += (_, _) => StopMotionUpdates();
        }

        private void StartMotionUpdates()
        {
            if (_motionUpdateTimer != null)
                return;

            _motionUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };

            _motionUpdateTimer.Tick += (_, _) =>
            {
                try
                {
                    IGamepad? gamepad = _parentViewModel?.ParentModel?.SelectedGamepad;
                    if (gamepad != null && MotionVisualizerInControl != null)
                    {
                        Vector3 gyroData = gamepad.GetMotionData(MotionInputId.Gyroscope);
                        gyroData = MotionUtils.ApplyGyroRotation(gyroData, ViewModel.GyroRotation);
                        MotionVisualizerInControl.MotionData = gyroData;
                    }
                }
                catch
                {
                    // Silently ignore errors (gamepad may have been disconnected)
                }
            };

            _motionUpdateTimer.Start();
        }

        private void StopMotionUpdates()
        {
            _motionUpdateTimer?.Stop();
            _motionUpdateTimer = null;
        }

        public static async Task Show(ControllerInputViewModel viewModel)
        {
            MotionInputView content = new(viewModel);

            ContentDialog contentDialog = new()
            {
                Title = LocaleManager.Instance[LocaleKeys.ControllerMotionTitle],
                PrimaryButtonText = LocaleManager.Instance[LocaleKeys.ControllerSettingsSave],
                SecondaryButtonText = string.Empty,
                CloseButtonText = LocaleManager.Instance[LocaleKeys.ControllerSettingsClose],
                Content = content,
            };
            contentDialog.PrimaryButtonClick += (_, _) =>
            {
                GamepadInputConfig config = viewModel.Config;
                config.Slot = content.ViewModel.Slot;
                config.Sensitivity = content.ViewModel.Sensitivity;
                config.GyroDeadzone = content.ViewModel.GyroDeadzone;
                config.AltSlot = content.ViewModel.AltSlot;
                config.DsuServerHost = content.ViewModel.DsuServerHost;
                config.DsuServerPort = content.ViewModel.DsuServerPort;
                config.EnableCemuHookMotion = content.ViewModel.EnableCemuHookMotion;
                config.MirrorInput = content.ViewModel.MirrorInput;
                config.GyroRotation = content.ViewModel.GyroRotation;
            };

            await contentDialog.ShowAsync();
        }
    }
}
