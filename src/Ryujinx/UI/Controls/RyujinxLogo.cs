using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Ryujinx.Ava.Common.Models;
using Ryujinx.Ava.Systems.Configuration;
using Ryujinx.Common;
using SkiaSharp;
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
            CurrentLogoBitmap.Event += CurrentLogoBitmapChanged_Event;
        }

        private void CurrentLogoBitmapChanged_Event(object _, ReactiveEventArgs<Bitmap> e)
        {
            Source = e.NewValue;
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
                Bitmap logoBitmap = GetBitmapForLogo(selectedIcon);
                if (logoBitmap != null)
                {
                    CurrentLogoBitmap.Value = logoBitmap;
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

        public static Bitmap GetBitmapForLogo(ApplicationIcon icon)
        {
            Stream activeIconStream = EmbeddedResources.GetStream(icon.FullPath);
            if (activeIconStream == null)
                return null;

            // SVG files need to be converted to an image first
            if (icon.FullPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                Stream pngStream = ConvertSvgToPng(activeIconStream);
                return new Bitmap(pngStream);
            }
            else
            {
                return new Bitmap(activeIconStream);
            }
        }

        private static Stream ConvertSvgToPng(Stream svgStream)
        {
            int width = 256;
            int height = 256;

            // Load SVG
            var svg = new SKSvg();
            svg.Load(svgStream);

            // Determine size
            var picture = svg.Picture;
            if (picture == null)
                throw new InvalidOperationException("Invalid SVG data");

            var picWidth = width > 0 ? width : (int)svg.Picture.CullRect.Width;
            var picHeight = height > 0 ? height : (int)svg.Picture.CullRect.Height;

            // Create bitmap
            using var bitmap = new SKBitmap(picWidth, picHeight);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            // Scale to fit
            float scaleX = (float)picWidth / svg.Picture.CullRect.Width;
            float scaleY = (float)picHeight / svg.Picture.CullRect.Height;
            canvas.Scale(scaleX, scaleY);

            canvas.DrawPicture(svg.Picture);
            canvas.Flush();

            // Encode PNG into memory stream
            var outputStream = new MemoryStream();
            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                data.SaveTo(outputStream);
            }

            outputStream.Position = 0;
            return outputStream;
        }
    }
}
