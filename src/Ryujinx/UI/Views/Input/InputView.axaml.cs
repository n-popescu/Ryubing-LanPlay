using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Systems.Configuration;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.ViewModels.Input;

namespace Ryujinx.Ava.UI.Views.Input
{
    public partial class InputView : RyujinxControl<InputViewModel>
    {
        private bool _dialogOpen;

        public InputView()
        {
            ReplaceViewModel(ConfigurationState.Instance.System.UseInputGlobalConfig);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            ViewModel?.RetargetKeyboardDriver(this);
        }

        public void SaveCurrentProfile()
        {
            ViewModel.Save();
        }

        public void ToggleLocalGlobalInput(bool enableConfigGlobal)
        {
            Dispose();
            ReplaceViewModel(enableConfigGlobal);
        }

        private void ReplaceViewModel(bool useGlobalConfig)
        {
            ViewModel = new InputViewModel(this, useGlobalConfig); // Create new Input Page with the selected input config scope.
            InitializeComponent();

            if (VisualRoot is not null)
            {
                ViewModel.RetargetKeyboardDriver(this);
            }
        }

        private async void PlayerIndexBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlayerIndexBox != null)
            {
                if (PlayerIndexBox.SelectedIndex != (int)ViewModel.PlayerId)
                {
                    PlayerIndexBox.SelectedIndex = (int)ViewModel.PlayerId;
                }
            }

            if (ViewModel.IsModified && !_dialogOpen)
            {
                _dialogOpen = true;

                UserResult result = await ContentDialogHelper.CreateDeniableConfirmationDialog(
                    LocaleManager.Instance[LocaleKeys.DialogControllerSettingsModifiedConfirmMessage],
                    LocaleManager.Instance[LocaleKeys.DialogControllerSettingsModifiedConfirmSubMessage],
                    LocaleManager.Instance[LocaleKeys.InputDialogYes],
                    LocaleManager.Instance[LocaleKeys.InputDialogNo],
                    LocaleManager.Instance[LocaleKeys.Cancel],
                    LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

                if (result == UserResult.Yes)
                {
                    ViewModel.Save();
                }

                _dialogOpen = false;

                if (result == UserResult.Cancel)
                {
                    if (e.AddedItems.Count > 0)
                    {
                        ViewModel.IsModified = true;
                        ViewModel.PlayerId = ((PlayerModel)e.AddedItems[0])!.Id;
                    }

                    return;
                }

                ViewModel.IsModified = false;
                ViewModel.PlayerId = ViewModel.PlayerIdChoose;

            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is FAComboBox faComboBox)
            {
                faComboBox.IsDropDownOpen = false;
            }

            ViewModel.RefreshModifiedState();
        }

        private async void AssignedDevicesButton_OnClick(object sender, RoutedEventArgs e)
        {
            await AssignedDevicesInputView.Show(ViewModel);
            ViewModel.RefreshModifiedState();
        }

        private void LinkProfileButton_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel?.LinkCurrentProfileToCurrentDevice();
        }

        private void LoadProfileButton_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel?.LoadProfile();
        }

        public void Dispose()
        {
            ViewModel.Dispose();
        }
    }
}
