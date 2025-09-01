using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FluentAvalonia.UI.Windowing;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Systems.Configuration;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Common;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Windows
{
    public abstract class StyleableAppWindow : AppWindow
    {
        public static async Task ShowAsync(StyleableAppWindow appWindow, Window owner = null)
        {
#if DEBUG
            appWindow.AttachDevTools(new KeyGesture(Key.F12, KeyModifiers.Control));
#endif
            await appWindow.ShowDialog(owner ?? RyujinxApp.MainWindow);
        }

        protected StyleableAppWindow(bool useCustomTitleBar = false, double? titleBarHeight = null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            TransparencyLevelHint = [WindowTransparencyLevel.None];

            LocaleManager.Instance.LocaleChanged += LocaleChanged;
            LocaleChanged();

            if (useCustomTitleBar)
            {
                TitleBar.ExtendsContentIntoTitleBar = !ConfigurationState.Instance.ShowOldUI;
                TitleBar.TitleBarHitTestType = ConfigurationState.Instance.ShowOldUI ? TitleBarHitTestType.Simple : TitleBarHitTestType.Complex;

                if (TitleBar.ExtendsContentIntoTitleBar && titleBarHeight != null)
                    TitleBar.Height = titleBarHeight.Value;
            }

            Icon = RyujinxLogo.CurrentLogoBitmap.Value;
            RyujinxLogo.CurrentLogoBitmap.Event += WindowIconChanged_Event;
        }

        private void LocaleChanged()
        {
            FlowDirection = LocaleManager.Instance.IsRTL() ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.SystemChrome | ExtendClientAreaChromeHints.OSXThickTitleBar;
        }

        private void WindowIconChanged_Event(object _, ReactiveEventArgs<Bitmap> rArgs) => UpdateIcon(rArgs.NewValue);
        private void UpdateIcon(Bitmap newIcon)
        {
            Icon = newIcon;
        }
    }

    public abstract class StyleableWindow : Window
    {
        public static async Task ShowAsync(StyleableWindow window, Window owner = null)
        {
#if DEBUG
            window.AttachDevTools(new KeyGesture(Key.F12, KeyModifiers.Control));
#endif
            await window.ShowDialog(owner ?? RyujinxApp.MainWindow);
        }

        protected StyleableWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            TransparencyLevelHint = [WindowTransparencyLevel.None];

            LocaleManager.Instance.LocaleChanged += LocaleChanged;
            LocaleChanged();

            Icon = new WindowIcon(RyujinxLogo.CurrentLogoBitmap.Value);
            RyujinxLogo.CurrentLogoBitmap.Event += WindowIconChanged_Event;
        }

        private void LocaleChanged()
        {
            FlowDirection = LocaleManager.Instance.IsRTL() ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.SystemChrome | ExtendClientAreaChromeHints.OSXThickTitleBar;
        }

        private void WindowIconChanged_Event(object _, ReactiveEventArgs<Bitmap> rArgs) => UpdateIcon(rArgs.NewValue);
        private void UpdateIcon(Bitmap newIcon)
        {
            Icon = new WindowIcon(newIcon);
        }
    }
}
