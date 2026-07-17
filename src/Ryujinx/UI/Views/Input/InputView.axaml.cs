using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Systems.Configuration;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.ViewModels.Input;
using System.ComponentModel;

namespace Ryujinx.Ava.UI.Views.Input
{
    public partial class InputView : RyujinxControl<InputViewModel>
    {
        private bool _dialogOpen;
        private bool _isEditingProfileName;
        private InputViewModel _subscribedViewModel;

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
            if (_subscribedViewModel != null)
            {
                _subscribedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
                _subscribedViewModel = null;
            }

            InputViewModel newViewModel = new InputViewModel(this, useGlobalConfig); // Create new Input Page with the selected input config scope.
            newViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
            _subscribedViewModel = newViewModel;

            ViewModel = newViewModel;
            InitializeComponent();

            SetupProfileBoxItemTemplate();

            _isEditingProfileName = false;
            UpdateProfileLinkIconVisibility();

            if (VisualRoot is not null)
            {
                ViewModel.RetargetKeyboardDriver(this);
            }
        }

        private void SetupProfileBoxItemTemplate()
        {
            var dataTemplate = new FuncDataTemplate<string>((profileName, scope) =>
            {
                var grid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto")
                };

                var textBlock = new TextBlock
                {
                    Text = profileName,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                    MaxWidth = 170
                };
                Grid.SetColumn(textBlock, 0);

                var linkIcon = new FASymbolIcon
                {
                    Symbol = FASymbol.Link,
                    FontSize = 12,
                    Opacity = 0.6,
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(linkIcon, 1);

                // Bind visibility to whether the profile is linked
                linkIcon.Bind(
                    IsVisibleProperty,
                    new Avalonia.Data.Binding(".")
                    {
                        Converter = ProfileNameLinkedConverter.Instance,
                        ConverterParameter = ViewModel
                    });

                grid.Children.Add(textBlock);
                grid.Children.Add(linkIcon);

                return grid;
            });

            ProfileBox.ItemTemplate = dataTemplate;
        }

        public void RefreshProfileBoxItemTemplate()
        {
            // Force the ComboBox to re-render its items
            var itemsSource = ProfileBox.ItemsSource;
            ProfileBox.ItemsSource = null;
            ProfileBox.ItemsSource = itemsSource;
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

        private async void ResetCurrentDeviceToDefaultsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.NeedsResetCurrentDeviceToDefaultsConfirmation())
            {
                ViewModel.ResetCurrentDeviceToDefaults();
                return;
            }

            Window owner = TopLevel.GetTopLevel(this) as Window;

            StackPanel content = new()
            {
                Spacing = 4,
                MaxWidth = 360,
            };

            content.Children.Add(new TextBlock
            {
                Text = LocaleManager.Instance[LocaleKeys.DialogControllerSettingsResetKeybindsConfirmMessage],
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 360,
            });

            content.Children.Add(new TextBlock
            {
                Text = LocaleManager.Instance[LocaleKeys.DialogControllerSettingsResetKeybindsConfirmSubMessage],
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 360,
            });

            FAContentDialog contentDialog = new FAContentDialog
            {
                Title = LocaleManager.Instance[LocaleKeys.RyujinxConfirm],
                PrimaryButtonText = LocaleManager.Instance[LocaleKeys.InputDialogYes],
                CloseButtonText = LocaleManager.Instance[LocaleKeys.InputDialogNo],
                DefaultButton = FAContentDialogButton.Primary,
                Content = content,
            }.ApplyStyles();

            FAContentDialogResult result = owner is not null
                ? await contentDialog.ShowAsync(owner)
                : await ContentDialogHelper.ShowAsync(contentDialog);

            if (result == FAContentDialogResult.Primary)
            {
                ViewModel.ResetCurrentDeviceToDefaults();
            }
        }

        private void LinkProfileButton_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel?.LinkCurrentProfileToCurrentDevice();
            RefreshProfileBoxItemTemplate();
        }

        private void LoadProfileButton_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel?.LoadProfile();
        }

        private void ViewModel_OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InputViewModel.IsProfileLinked))
            {
                UpdateProfileLinkIconVisibility();
            }
        }

        private void ProfileBox_OnGotFocus(object sender, RoutedEventArgs e)
        {
            _isEditingProfileName = true;
            UpdateProfileLinkIconVisibility();
        }

        private void ProfileBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            _isEditingProfileName = false;
            UpdateProfileLinkIconVisibility();
        }

        private void UpdateProfileLinkIconVisibility()
        {
            if (ProfileLinkIcon == null)
            {
                return;
            }

            ProfileLinkIcon.IsVisible = !_isEditingProfileName && (ViewModel?.IsProfileLinked ?? false);
        }

        public void Dispose()
        {
            if (_subscribedViewModel != null)
            {
                _subscribedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
                _subscribedViewModel = null;
            }

            ViewModel.Dispose();
        }
    }
}
