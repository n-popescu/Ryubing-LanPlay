using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Systems.Configuration;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.UI.Views.Main;
using Ryujinx.Ava.UI.Windows;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace Ryujinx.Ava.UI.Helpers
{
    /// <summary>
    /// Mirrors <see cref="MainMenuBarView"/>'s in-window <c>Menu</c> tree as a NativeMenu so
    /// that, on macOS, the application's menus appear in the system menu bar at the top of
    /// the screen instead of inside the window.
    ///
    /// The mirroring is structural and reactive: every <see cref="NativeMenuItem"/> is bound
    /// to the corresponding <see cref="MenuItem"/>'s Header, Command, CommandParameter,
    /// IsEnabled, IsVisible, InputGesture, and (for items rendered as checkboxes via a
    /// CheckBox icon) the IsChecked state. Because the bindings forward changes from the
    /// XAML <c>MenuItem</c>s, the in-window menu is the single source of truth — adding,
    /// removing, or reordering items in <c>MainMenuBarView.axaml</c> automatically updates
    /// the macOS menu without any code changes here.
    ///
    /// Items added at runtime via <c>ItemsSource</c> (the language list and shown-file-type
    /// list) are picked up by walking <c>MenuItem.Items</c>. Locale changes trigger a full
    /// rebuild, since headers and the language list both shift.
    /// </summary>
    internal sealed class MacOSNativeMenuBuilder
    {
        private readonly MainMenuBarView _view;
        private readonly MainWindow _window;
        private readonly List<IDisposable> _bindings = new();
        private static MacOSNativeMenuBuilder _current;

        private static bool? s_useNativeMenuBar;

        /// <summary>
        /// Whether the macOS native menu bar should be used. Cached on first read so the
        /// embedded menu strip and the native menu agree on a single mode for the
        /// lifetime of the process; toggling the setting takes effect after a restart.
        /// </summary>
        public static bool UseNativeMenuBar
        {
            get
            {
                if (!OperatingSystem.IsMacOS())
                    return false;

                s_useNativeMenuBar ??= ConfigurationState.Instance?.UI.EnableMacOSNativeMenuBar.Value == true;

                return s_useNativeMenuBar.Value;
            }
        }

        public static void ApplyMenuBarMode(MainMenuBarView view)
        {
            if (!OperatingSystem.IsMacOS())
                return;

            if (UseNativeMenuBar)
            {
                TryAttach(view);
            }
            else
            {
                _current?.Detach();
            }
        }

        public static MacOSNativeMenuBuilder TryAttach(MainMenuBarView view)
        {
            if (!UseNativeMenuBar)
                return null;
            if (view.Window is null || view.DataContext is not MainWindowViewModel)
                return null;
            // Don't re-attach if a native menu is already installed.
            if (NativeMenu.GetMenu(view.Window) is not null)
                return null;

            MacOSNativeMenuBuilder builder = new(view);
            builder.Build();
            _current = builder;
            return builder;
        }

        private MacOSNativeMenuBuilder(MainMenuBarView view)
        {
            _view = view;
            _window = view.Window;
        }

        private void Build()
        {
            NativeMenu.SetMenu(_window, MirrorItemsControl(_view.Menu));

            LocaleManager.Instance.LocaleChanged += OnLocaleChanged;
            _window.Closed += OnWindowClosed;
        }

        private void OnWindowClosed(object sender, EventArgs e) => Detach();

        private void Detach()
        {
            LocaleManager.Instance.LocaleChanged -= OnLocaleChanged;
            _window.Closed -= OnWindowClosed;
            DisposeBindings();
            if (_current == this)
                _current = null;
        }

        private void OnLocaleChanged()
        {
            // Locale change can swap headers and the language sublist, so do a full rebuild.
            // Marshal to UI thread defensively.
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(OnLocaleChanged);
                return;
            }

            DisposeBindings();
            NativeMenu.SetMenu(_window, MirrorItemsControl(_view.Menu));
        }

        private void DisposeBindings()
        {
            foreach (IDisposable d in _bindings)
                d.Dispose();
            _bindings.Clear();
        }

        // --- Generic mirror -------------------------------------------------

        private NativeMenu MirrorItemsControl(ItemsControl source)
        {
            NativeMenu native = new();
            foreach (object child in source.Items)
            {
                NativeMenuItemBase mirrored = MirrorItem(child);
                if (mirrored is not null)
                    native.Add(mirrored);
            }
            return native;
        }

        private NativeMenuItemBase MirrorItem(object item)
        {
            if (item is Separator)
                return new NativeMenuItemSeparator();
            if (item is not MenuItem mi)
                return null;

            NativeMenuItem native = new();

            // Header (object → string).
            _bindings.Add(native.Bind(NativeMenuItem.HeaderProperty,
                mi.GetObservable(HeaderedSelectingItemsControl.HeaderProperty)
                    .Select(h => h?.ToString() ?? string.Empty)));

            // Command + parameter — the same ICommand instance the in-window menu uses.
            _bindings.Add(native.Bind(NativeMenuItem.CommandProperty,
                mi.GetObservable(MenuItem.CommandProperty)));
            _bindings.Add(native.Bind(NativeMenuItem.CommandParameterProperty,
                mi.GetObservable(MenuItem.CommandParameterProperty)));

            // Enabled + visible state piggy-backs on the XAML bindings.
            _bindings.Add(native.Bind(NativeMenuItem.IsEnabledProperty,
                mi.GetObservable(InputElement.IsEnabledProperty)));
            _bindings.Add(native.Bind(NativeMenuItem.IsVisibleProperty,
                mi.GetObservable(Visual.IsVisibleProperty)));

            // Keyboard shortcut.
            if (mi.InputGesture is not null)
                native.Gesture = mi.InputGesture;

            // Toggle items in the XAML use a CheckBox in the Icon slot. Surface that as
            // a native checkmark item bound to the same IsChecked observable.
            if (mi.Icon is CheckBox cb)
            {
                native.ToggleType = NativeMenuItemToggleType.CheckBox;
                _bindings.Add(native.Bind(NativeMenuItem.IsCheckedProperty,
                    cb.GetObservable(ToggleButton.IsCheckedProperty)
                        .Select(c => c == true)));
            }

            // Recurse into submenu items (covers both static XAML children and any
            // ItemsSource the code-behind has set, since both surface via Items).
            bool hasChildren = false;
            foreach (object _ in mi.Items)
            {
                hasChildren = true;
                break;
            }

            if (hasChildren)
                native.Menu = MirrorItemsControl(mi);

            return native;
        }
    }
}
