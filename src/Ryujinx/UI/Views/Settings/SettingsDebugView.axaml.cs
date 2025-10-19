using Avalonia.Controls;
using Avalonia.Interactivity;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using System;
using System.IO;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsDebugView : UserControl
    {
        public SettingsDebugView()
        {
            InitializeComponent();
        }

        private async void PurgeAllSharedCaches_OnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var appList = RyujinxApp.MainWindow.ViewModel.Applications;

            if (appList.Count == 0)
            {
                return;
            }

            UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                LocaleManager.Instance[LocaleKeys.DialogWarning],
                LocaleManager.Instance[LocaleKeys.DialogDeleteAllCaches],
                LocaleManager.Instance[LocaleKeys.InputDialogYes],
                LocaleManager.Instance[LocaleKeys.InputDialogNo],
                LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);
            bool hasErrors = false;

            if (result != UserResult.Yes)
            {
                return;
            }

            foreach (var application in appList)
            {
                DirectoryInfo cacheDir = new(Path.Combine(AppDataManager.GamesDirPath, application.IdString, "cache"));

                if (!cacheDir.Exists)
                {
                    continue;
                }

                foreach (var finfo in cacheDir.EnumerateDirectories())
                {
                    try
                    {
                        finfo.Delete(true);

                    }
                    catch (Exception ex)
                    {
                        Logger.Error?.Print(LogClass.Application, $"Failed to purge shader cache for {application.Name}({application.IdString}): {ex}");
                        hasErrors = true;
                    }
                }
            }

            if (hasErrors)
            {
                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogDeleteAllCachesErrorMessage]);
            }
        }
    }
}
