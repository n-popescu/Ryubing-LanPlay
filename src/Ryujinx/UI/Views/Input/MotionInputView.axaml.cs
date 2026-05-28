using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models.Input;
using Ryujinx.Ava.UI.ViewModels.Input;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Input;
using Ryujinx.Input.Motion;
using System;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Views.Input
{
    public partial class MotionInputView : RyujinxControl<MotionInputViewModel>
    {
        private ControllerInputViewModel _parentController;

        public MotionInputView()
        {
            InitializeComponent();
        }

        public MotionInputView(ControllerInputViewModel viewModel)
        {
            _parentController = viewModel;
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
                EnablePassiveCalibration = config.EnablePassiveCalibration,
            };

            InitializeComponent();
        }

        private async void OnCalibrateClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            IGamepad gamepad = _parentController?.ParentModel?.SelectedGamepad;
            if (gamepad == null || !gamepad.Features.HasFlag(GamepadFeaturesFlag.Motion))
            {
                return;
            }

            // The wizard runs a detached calibrator so it doesn't disturb the live pipeline.
            // On success we persist the new bias to the store; the live MotionInput picks it up
            // on the next UpdateMotionInput call (e.g. on save or restart). Updating the live
            // calibrator instantly would require plumbing a callback from settings to NpadController,
            // which is deferred to a follow-up.
            var calibrator = new GyroCalibrator();
            var vm = new GyroCalibrationViewModel(gamepad, calibrator, MotionInputId.Gyroscope, ViewModel.GyroDeadzone);

            calibrator.ManualCalibrationCompleted += result =>
            {
                if (!result.Success)
                {
                    return;
                }
                string key = gamepad.GetCalibrationKey(MotionInputId.Gyroscope);
                if (string.IsNullOrEmpty(key))
                {
                    return;
                }
                GyroCalibrationStore store = GyroCalibrationStoreProvider.Instance;
                store?.Set(key, new GyroCalibrationEntry
                {
                    Name = gamepad.Name,
                    Bias = result.Bias,
                    CalibratedAt = DateTime.UtcNow,
                    Source = GyroCalibrationSource.Manual,
                });
                store?.Save();
            };

            vm.ApplyDeadzoneRequested += suggested =>
            {
                // Round to 2 decimals to match the existing slider precision (GyroDeadzone is a double).
                ViewModel.GyroDeadzone = System.Math.Round(suggested, 2);
            };

            var window = new GyroCalibrationWindow(vm);

            // Get the parent window for the ShowDialog call.
            if (this.GetVisualRoot() is Avalonia.Controls.Window owner)
            {
                await window.ShowDialog(owner);
            }
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
                config.EnablePassiveCalibration = content.ViewModel.EnablePassiveCalibration;
                config.MirrorInput = content.ViewModel.MirrorInput;
            };

            await ContentDialogHelper.ShowAsync(contentDialog);
        }
    }
}
