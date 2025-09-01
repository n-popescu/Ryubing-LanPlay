using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Ryujinx.Ava.Common.Models;
using Ryujinx.Ava.Systems.Configuration;
using Ryujinx.Common;
using Svg.Skia;
using System;
using System.IO;
using System.Linq;

namespace Ryujinx.Ava.UI.Controls
{
    public class RyujinxLogo : Image
    {
        public static ReactiveObject<Bitmap> CurrentLogoBitmap { get; private set; } = new();

        public RyujinxLogo()
        {
            Margin = new Thickness(7, 7, 7, 0);
            Height = 25;
            Width = 25;
            Source = CurrentLogoBitmap.Value;
            IsVisible = !ConfigurationState.Instance.ShowOldUI;
            ConfigurationState.Instance.UI.SelectedWindowIcon.Event += WindowIconChanged_Event;
        }

        public static void RefreshAppIconFromSettings()
        {
            SetNewAppIcon(ConfigurationState.Instance.UI.SelectedWindowIcon.Value);
        }

        private static void SetNewAppIcon(string newIconName)
        {
            string defaultIconName = "Bordered Ryupride";
            if (string.IsNullOrEmpty(newIconName))
            {
                SetDefaultAppIcon(defaultIconName);
            }

            ApplicationIcon selectedIcon = RyujinxApp.AvailableApplicationIcons.FirstOrDefault(x => x.Name == newIconName);
            if (selectedIcon == null)
            {
                // Always try to fallback to "Bordered Ryupride" as a default
                // If not found, fallback to first found icon
                if (newIconName != defaultIconName)
                {
                    SetDefaultAppIcon(defaultIconName);
                    return;
                }

                if (RyujinxApp.AvailableApplicationIcons.Count > 0)
                {
                    SetDefaultAppIcon(RyujinxApp.AvailableApplicationIcons.First().Name);
                    return;
                }
            }

            Stream activeIconStream = EmbeddedResources.GetStream(selectedIcon.FullPath);
            if (activeIconStream != null)
            {
                // SVG files need to be converted to an image first
                if (selectedIcon.FullPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: Convert SVG to a bitmap
                    using SKSvg svg = new();
                }
                else
                {
                    CurrentLogoBitmap.Value = new Bitmap(activeIconStream);
                }
            }
        }

        private static void SetDefaultAppIcon(string defaultIconName)
        {
            // Doing this triggers the WindowIconChanged_Event, which will then
            // call SetNewAppIcon again
            ConfigurationState.Instance.UI.SelectedWindowIcon.Value = defaultIconName;
        }
        
        private void WindowIconChanged_Event(object _, ReactiveEventArgs<string> rArgs) => SetNewAppIcon(rArgs.NewValue);
    }
}
