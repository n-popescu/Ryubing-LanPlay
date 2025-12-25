using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using UserProfile = Ryujinx.Ava.UI.Models.UserProfile;
using Avalonia.Platform.Storage;
using Ryujinx.HLE.FileSystem;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using Avalonia.VisualTree;

namespace Ryujinx.Ava.UI.Views.User
{
    public partial class UserEditorView : RyujinxControl<TempProfile>
    {
        private NavigationDialogHost _parent;
        private ContentManager _contentManager;
        private UserProfile _profile;
        private TempProfile _tempProfile;
        private bool _isNewUser;
        public static uint MaxProfileNameLength => 0x20;
        public bool IsDeletable => _profile.UserId != AccountManager.DefaultUserId;
        public string UserEditorTitle => LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.UserEditorTitle, _profile.Name);
        public UserEditorView()
        {
            InitializeComponent();
            AddHandler(Frame.NavigatedToEvent, (s, e) =>
            {
                NavigatedTo(e);
            }, RoutingStrategies.Direct);
        }

        private void NavigatedTo(NavigationEventArgs arg)
        {
            if (Program.PreviewerDetached)
            {
                switch (arg.NavigationMode)
                {
                    case NavigationMode.New:
                        (NavigationDialogHost parent, UserProfile profile, bool isNewUser) = ((NavigationDialogHost parent, UserProfile profile, bool isNewUser))arg.Parameter;
                        _isNewUser = isNewUser;
                        _profile = profile;
                        ViewModel = new TempProfile(_profile);
                        _tempProfile = ViewModel; // <-- this is critical

                        _parent = parent;

                        _contentManager = _parent.ContentManager;
                        ViewModel.FirmwareFound = _contentManager.GetCurrentFirmwareVersion() != null;

                        break;
                }

                ((ContentDialog)_parent.Parent).Title = $"{LocaleManager.Instance[LocaleKeys.UserProfileWindowTitle]} - " +
                                                        $"{(_isNewUser ? LocaleManager.Instance[LocaleKeys.UserEditorTitleNewUser] : UserEditorTitle)}";

                AddPictureButton.IsVisible = _isNewUser;
                ChangePictureButton.IsVisible = !_isNewUser;
                IdLabel.IsVisible = _profile != null;
                IdText.IsVisible = _profile != null;
                if (!_isNewUser && IsDeletable)
                {
                    DeleteButton.IsVisible = true;
                }
                else
                {
                    DeleteButton.IsVisible = false;
                }
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isNewUser)
            {
                if (ViewModel.Name != string.Empty || ViewModel.Image != null)
                {
                    if (await ContentDialogHelper.CreateChoiceDialog(
                            LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesTitle],
                            LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesMessage],
                            LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesSubMessage]))
                    {
                        _parent?.GoBack();
                    }
                }
                else
                {
                    _parent?.GoBack();
                }
            }
            else
            {
                if (_profile.Name != ViewModel.Name || _profile.Image != ViewModel.Image)
                {
                    if (await ContentDialogHelper.CreateChoiceDialog(
                            LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesTitle],
                            LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesMessage],
                            LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesSubMessage]))
                    {
                        _parent?.GoBack();
                    }
                }
                else
                {
                    _parent?.GoBack();
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            _parent.DeleteUser(_profile);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DataValidationErrors.ClearErrors(NameBox);

            if (string.IsNullOrWhiteSpace(ViewModel.Name))
            {
                DataValidationErrors.SetError(NameBox, new DataValidationException(LocaleManager.Instance[LocaleKeys.UserProfileEmptyNameError]));

                return;
            }

            if (ViewModel.Image == null)
            {
                _parent.Navigate(typeof(UserProfileImageSelectorView), (_parent, ViewModel));

                return;
            }

            if (_profile != null && !_isNewUser)
            {
                _profile.Name = ViewModel.Name;
                _profile.Image = ViewModel.Image;
                _profile.UpdateState();
                _parent.AccountManager.SetUserName(_profile.UserId, _profile.Name);
                _parent.AccountManager.SetUserImage(_profile.UserId, _profile.Image);
            }
            else if (_isNewUser)
            {
                _parent.AccountManager.AddUser(ViewModel.Name, ViewModel.Image, ViewModel.UserId);
            }
            else
            {
                return;
            }

            _parent?.GoBack();
        }

        public void SelectProfileImage()
        {
            _parent.Navigate(typeof(UserProfileImageSelectorView), (_parent, ViewModel));
        }

        private void ChangePictureButton_Click(object sender, RoutedEventArgs e)
        {
            if (_profile != null || _isNewUser)
            {
                SelectProfileImage();
            }
        }

        private async void SelectFirmwareImage_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.FirmwareFound)
            {
                _parent.Navigate(typeof(UserFirmwareAvatarSelectorView), (_parent, _tempProfile));
            }
        }

        private async void Import_OnClick(object sender, RoutedEventArgs e)
        {
            var result = await ((Window)this.GetVisualRoot()!).StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions 
            {
                Title = LocaleManager.Instance[LocaleKeys.LoadSupportedImageFormatDialogTitle],
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(LocaleManager.Instance[LocaleKeys.AllSupportedFormats])
                    {
                        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp"],
                        AppleUniformTypeIdentifiers = ["public.jpeg", "public.png", "com.microsoft.bmp"],
                        MimeTypes = ["image/jpeg", "image/png", "image/bmp"],
                    },
                    new("JPG")
                    {
                        Patterns = ["*.jpg"],
                        AppleUniformTypeIdentifiers = ["public.jpeg"],
                        MimeTypes = ["image/jpeg"],
                    },
                    new("JPEG")
                    {
                        Patterns = ["*.jpeg"],
                        AppleUniformTypeIdentifiers = ["public.jpeg"],
                        MimeTypes = ["image/jpeg"],
                    },
                    new("PNG")
                    {
                        Patterns = ["*.png"],
                        AppleUniformTypeIdentifiers = ["public.png"],
                        MimeTypes = ["image/png"],
                    },
                    new("BMP")
                    {
                        Patterns = ["*.bmp"],
                        AppleUniformTypeIdentifiers = ["com.microsoft.bmp"],
                        MimeTypes = ["image/bmp"],
                    },
                },
            });

            if (result.Count == 0)
                return;

            if (DataContext is not TempProfile temp)
                return;

            temp.Image = ProcessProfileImage(File.ReadAllBytes(result[0].Path.LocalPath));

            if (_profile != null)
                _profile.Image = temp.Image;
        }

        private static byte[] ProcessProfileImage(byte[] buffer)
        {
            using SKBitmap bitmap = SKBitmap.Decode(buffer);

            SKBitmap resizedBitmap = bitmap.Resize(new SKImageInfo(256, 256), SKFilterQuality.High);

            using MemoryStream streamJpg = new();

            if (resizedBitmap != null)
            {
                using SKImage image = SKImage.FromBitmap(resizedBitmap);
                using SKData dataJpeg = image.Encode(SKEncodedImageFormat.Jpeg, 100);

                dataJpeg.SaveTo(streamJpg);
            }

            return streamJpg.ToArray();
        }
    }
}