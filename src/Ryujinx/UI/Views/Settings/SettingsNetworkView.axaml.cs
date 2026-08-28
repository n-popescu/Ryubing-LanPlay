using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Gommon;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.Utilities;
using System;
using System.Collections.Generic;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsNetworkView : RyujinxControl<SettingsViewModel>
    {
        private readonly Random _random;

        public SettingsNetworkView()
        {
            _random = new Random();
            InitializeComponent();
        }

        private void GenLdnPassButton_OnClick(object sender, RoutedEventArgs e)
        {
            byte[] code = new byte[4];
            _random.NextBytes(code);
            ViewModel.LdnPassphrase = $"Ryujinx-{BitConverter.ToUInt32(code):x8}";
        }

        private void ClearLdnPassButton_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.LdnPassphrase = string.Empty;
        }

        private void TestLanPlayButton_OnClick(object sender, RoutedEventArgs e)
        {
            _ = ViewModel.TestLanPlayConnection();
        }

        private async void BrowsePrivateServerCaButton_OnClick(object sender, RoutedEventArgs e)
        {
            // A CA bundle is a PEM file, so ".pem" and ".crt" cover what a private server's
            // tooling actually produces. The filter is not enforcement -- the path is read at
            // handshake time and a file that is not a PEM bundle is reported there, by
            // PrivateServerTrust, rather than being silently ignored.
            Optional<IStorageFile> result = await ((Window)TopLevel.GetTopLevel(this))!
                .StorageProvider.OpenSingleFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = LocaleManager.Instance[LocaleKeys.SettingsTabNetworkPrivateServerCa],
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new(LocaleManager.Instance[LocaleKeys.SettingsTabNetworkPrivateServerCaFileType])
                        {
                            Patterns = ["*.pem", "*.crt", "*.cer"],
                            MimeTypes = ["application/x-pem-file", "application/x-x509-ca-cert"],
                        },
                    },
                });

            if (result.HasValue)
            {
                ViewModel.PrivateServerCaBundle = result.Value.Path.LocalPath;
            }
        }
    }
}
