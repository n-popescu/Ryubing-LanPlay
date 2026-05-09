using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Models.Input;
using Ryujinx.Ava.UI.ViewModels.Input;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Views.Input
{
    public partial class AssignedDevicesInputView : UserControl
    {
        public AssignedDevicesInputView()
        {
            InitializeComponent();
        }

        public AssignedDevicesInputView(InputViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }

        public static async Task Show(InputViewModel viewModel)
        {
            AssignedDevicesInputView content = new(viewModel);

            ContentDialog contentDialog = new()
            {
                Title = LocaleManager.Instance[LocaleKeys.ControllerSettingsAssignedInputDevices],
                PrimaryButtonText = LocaleManager.Instance[LocaleKeys.ControllerSettingsSave],
                SecondaryButtonText = string.Empty,
                CloseButtonText = LocaleManager.Instance[LocaleKeys.ControllerSettingsClose],
                Content = content,
            };

            ContentDialogResult result = await contentDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                viewModel.Save();
            }
        }

        private void AssignedDeviceCheckBox_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { DataContext: PlayerInputDeviceAssignmentItem item } checkBox)
            {
                _viewModel?.ToggleAssignedPlayerInputDevice(item, checkBox.IsChecked == true);
            }
        }

        private void DeviceRow_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.Source is Control control && control.FindAncestorOfType<CheckBox>() != null)
            {
                return;
            }

            if (sender is Border { DataContext: PlayerInputDeviceAssignmentItem item })
            {
                _viewModel?.ToggleAssignedPlayerInputDevice(item, !item.IsAssigned);
            }
        }

        private InputViewModel _viewModel => DataContext as InputViewModel;
    }
}
