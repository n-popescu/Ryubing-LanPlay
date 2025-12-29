using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.HLE.FileSystem;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using UserProfile = Ryujinx.Ava.UI.Models.UserProfile;

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
            AddHandler(Frame.NavigatedToEvent, (s, e) => NavigatedTo(e), RoutingStrategies.Direct);
        }

        private void NavigatedTo(NavigationEventArgs arg)
        {
            if (!Program.PreviewerDetached)
                return;

            if (arg.NavigationMode == NavigationMode.New)
            {
                (NavigationDialogHost parent, UserProfile profile, bool isNewUser) =
                    ((NavigationDialogHost parent, UserProfile profile, bool isNewUser))arg.Parameter;

                _parent = parent;
                _profile = profile;
                _isNewUser = isNewUser;

                DataValidationErrors.ClearErrors(NameBox); 
                DataValidationErrors.ClearErrors(ImageBox);
                ImageBox.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("AppListHoverBackgroundColor"));

                ViewModel = new TempProfile(_profile);
                _tempProfile = ViewModel;

                _contentManager = _parent.ContentManager;
                ViewModel.FirmwareFound = _contentManager.GetCurrentFirmwareVersion() != null;
            }

            ((ContentDialog)_parent.Parent).Title =
                $"{LocaleManager.Instance[LocaleKeys.UserProfileWindowTitle]} - " +
                $"{(_isNewUser ? LocaleManager.Instance[LocaleKeys.UserEditorTitleNewUser] : UserEditorTitle)}";

            bool hasProfile = _profile != null;
            IdLabel.IsVisible = hasProfile;
            IdText.IsVisible = hasProfile;

            DeleteButton.IsVisible = !_isNewUser && IsDeletable;
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            bool hasUnsavedChanges =
                _isNewUser
                    ? (ViewModel.Name != string.Empty || ViewModel.Image != null)
                    : (_profile.Name != ViewModel.Name || _profile.Image != ViewModel.Image);

            if (hasUnsavedChanges)
            {
                bool confirm = await ContentDialogHelper.CreateChoiceDialog(
                    LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesTitle],
                    LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesMessage],
                    LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesSubMessage]);

                if (confirm)
                    _parent?.GoBack();
            }
            else
            {
                _parent?.GoBack();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            _parent.DeleteUser(_profile);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DataValidationErrors.ClearErrors(NameBox);
            DataValidationErrors.ClearErrors(ImageBox);
            ImageBox.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("AppListHoverBackgroundColor"));

            bool nameEmpty = string.IsNullOrWhiteSpace(ViewModel.Name); 
            bool imageMissing = ViewModel.Image == null;

            if (nameEmpty && imageMissing) 
            { 
                DataValidationErrors.SetError(NameBox, new DataValidationException(LocaleManager.Instance[LocaleKeys.UserProfileEmptyNameError])); 
                DataValidationErrors.SetError(ImageBox, new DataValidationException(""));
                ImageBox.BorderBrush = Brush.Parse("#ff99a4");
                return; 
            }
            else if (nameEmpty)
            {
                DataValidationErrors.SetError(NameBox,new DataValidationException(LocaleManager.Instance[LocaleKeys.UserProfileEmptyNameError]));
                return;
            }
            else if (imageMissing)
            {
                DataValidationErrors.SetError(ImageBox, new DataValidationException(""));
                ImageBox.BorderBrush = Brush.Parse("#ff99a4");
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

        private void SelectFirmwareImage_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.FirmwareFound)
            {
                _parent.Navigate(typeof(UserFirmwareAvatarSelectorView), (_parent, _tempProfile));
            }
        }

        private async void Import_OnClick(object sender, RoutedEventArgs e)
        {
            var window = (Window)this.GetVisualRoot()!;
            var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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
                },
            });

            if (result.Count == 0 || DataContext is not TempProfile temp)
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