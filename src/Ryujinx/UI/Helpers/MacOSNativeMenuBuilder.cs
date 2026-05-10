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
using System.Windows.Input;

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
        private readonly List<NativeMenu> _trackedMenus = new();
        private readonly List<Window> _windows = new();
        private static MacOSNativeMenuBuilder _current;
        private NativeMenu _root;

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
            if (_current is not null)
            {
                _current.ApplyToWindow(view.Window);
                return _current;
            }
            if (NativeMenu.GetMenu(view.Window) is not null)
                return null;

            MacOSNativeMenuBuilder builder = new(view);
            builder.Build();
            _current = builder;
            return builder;
        }

        /// <summary>
        /// Re-applies the active builder's menu to a newly-shown window so child windows
        /// (Settings, Compatibility List, LDN Game List, etc.) keep showing the menu while
        /// they are key. macOS's menu bar is per-application but Avalonia tracks ownership
        /// per-window, so each window must have the same NativeMenu instance attached.
        /// </summary>
        public static void TryApplyToWindow(Window window)
        {
            if (!UseNativeMenuBar || window is null)
                return;

            _current?.ApplyToWindow(window);
        }

        private MacOSNativeMenuBuilder(MainMenuBarView view)
        {
            _view = view;
            _window = view.Window;
        }

        private void Build()
        {
            _root = MirrorItemsControl(_view.Menu);
            ApplyToWindow(_window);

            LocaleManager.Instance.LocaleChanged += OnLocaleChanged;
            _window.Closed += OnWindowClosed;

            // Avalonia hardcodes the macOS Apple-menu "Quit" item to just "Quit"
            // (see AvaloniaNativeMenuExporter.PopulateStandardOSXMenuItems). Patch
            // it to "Quit AppName" via NSApp.mainMenu. This must be deferred until
            // after the exporter has built the menu in response to SetMenu above.
            if (OperatingSystem.IsMacOS())
            {
                string appName = Application.Current?.Name ?? "Ryujinx";
                Dispatcher.UIThread.Post(() =>
                {
                    if (OperatingSystem.IsMacOS())
                        AppleMenu.RenameQuitItem($"Quit {appName}");
                }, DispatcherPriority.Background);
            }
        }

        private void OnWindowClosed(object sender, EventArgs e) => Detach();

        private void Detach()
        {
            LocaleManager.Instance.LocaleChanged -= OnLocaleChanged;
            _window.Closed -= OnWindowClosed;
            ClearFromWindows();
            DisposeBindings();
            DisposeMenuEventHandlers();
            if (_current == this)
                _current = null;
        }

        private void ApplyToWindow(Window window)
        {
            if (_root is null || window is null || NativeMenu.GetMenu(window) is not null)
                return;

            NativeMenu.SetMenu(window, _root);
            _windows.Add(window);
        }

        private void ClearFromWindows()
        {
            foreach (Window window in _windows)
            {
                NativeMenu.SetMenu(window, null);
            }

            _windows.Clear();
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

            // Avalonia's macOS NSMenu exporter tracks the NativeMenu instance once
            // installed and throws "The menu being updated does not match" if SetMenu
            // is called with a different instance. Mutate the existing root in place
            // instead — clear its items, dispose old bindings, and re-walk the source.
            DisposeBindings();
            DisposeMenuEventHandlers();
            TrackMenu(_root);
            _root.Items.Clear();
            foreach (object child in _view.Menu.Items)
            {
                NativeMenuItemBase mirrored = MirrorItem(child);
                if (mirrored is not null)
                    _root.Add(mirrored);
            }
        }

        private void DisposeBindings()
        {
            foreach (IDisposable d in _bindings)
                d.Dispose();
            _bindings.Clear();
        }

        private void DisposeMenuEventHandlers()
        {
            foreach (NativeMenu menu in _trackedMenus)
            {
                menu.NeedsUpdate -= OnNativeMenuNeedsUpdate;
                menu.Opening -= OnNativeMenuNeedsUpdate;
            }
            _trackedMenus.Clear();
        }

        // --- Generic mirror -------------------------------------------------

        private NativeMenu CreateTrackedMenu()
        {
            NativeMenu menu = new();
            TrackMenu(menu);
            return menu;
        }

        private void TrackMenu(NativeMenu menu)
        {
            // Several ViewModel flags (Amiibo / Skylander) are populated lazily by
            // AttachedToVisualTree on the embedded MenuItems, which never fires for
            // their NativeMenuItem mirrors. Hook NeedsUpdate / Opening on every
            // NativeMenu we build so we can re-pull the same state when the user
            // opens any menu.
            menu.NeedsUpdate += OnNativeMenuNeedsUpdate;
            menu.Opening += OnNativeMenuNeedsUpdate;
            _trackedMenus.Add(menu);
        }

        private void OnNativeMenuNeedsUpdate(object sender, EventArgs e)
        {
            _view.RefreshDynamicNativeMenuState();
        }

        private NativeMenu MirrorItemsControl(ItemsControl source)
        {
            NativeMenu native = CreateTrackedMenu();
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

            // Visible state piggy-backs on the XAML binding (used to swap the two
            // Actions top-level menus based on game-running state, etc.).
            _bindings.Add(native.Bind(NativeMenuItem.IsVisibleProperty,
                mi.GetObservable(Visual.IsVisibleProperty)));

            if (HasSubMenu(mi))
            {
                // Pure submenu container. Avalonia's NSMenuItem validator returns YES
                // automatically for items with submenus, so we don't need IsEnabled
                // wired up here.
                native.Menu = MirrorItemsControl(mi);
            }
            else
            {
                // Leaf: command, command parameter, gesture, IsEnabled, and toggle state.
                _bindings.Add(native.Bind(NativeMenuItem.IsEnabledProperty,
                    mi.GetObservable(InputElement.IsEnabledProperty)));

                // Defer command execution to the UI thread via Dispatcher.Post. NSMenu
                // invokes the action synchronously on the menu-tracking runloop while
                // the menu is still open, and a long-running command (Install Keys, Mii
                // Editor load, etc.) on that thread freezes the entire menu and the
                // app. The deferred wrapper also lets the underlying ICommand reference
                // change at runtime — useful for items whose Command is wired up
                // post-construction by the code-behind.
                native.Command = new DeferredNativeMenuCommand(
                    () => mi.Command,
                    () => mi.CommandParameter);

                if (mi.InputGesture is not null)
                    native.Gesture = mi.InputGesture;

                // Toggle items in the XAML use a CheckBox in the Icon slot. Surface
                // that as a native checkmark item bound to the same IsChecked observable.
                if (mi.Icon is CheckBox cbIcon)
                {
                    native.ToggleType = NativeMenuItemToggleType.CheckBox;
                    _bindings.Add(native.Bind(NativeMenuItem.IsCheckedProperty,
                        cbIcon.GetObservable(ToggleButton.IsCheckedProperty)
                            .Select(c => c == true)));
                }
            }

            return native;
        }

        private static bool HasSubMenu(MenuItem menuItem)
        {
            return menuItem.HasSubMenu || menuItem.ItemCount > 0 || menuItem.ItemsSource is not null;
        }

        private sealed class DeferredNativeMenuCommand : ICommand
        {
            private readonly Func<ICommand> _commandAccessor;
            private readonly Func<object> _parameterAccessor;

            public DeferredNativeMenuCommand(Func<ICommand> commandAccessor, Func<object> parameterAccessor)
            {
                _commandAccessor = commandAccessor;
                _parameterAccessor = parameterAccessor;
            }

            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object parameter)
            {
                ICommand command = _commandAccessor();
                object commandParameter = _parameterAccessor();

                return command?.CanExecute(commandParameter) == true;
            }

            public void Execute(object parameter)
            {
                ICommand command = _commandAccessor();
                object commandParameter = _parameterAccessor();

                if (command?.CanExecute(commandParameter) != true)
                    return;

                Dispatcher.UIThread.Post(() =>
                {
                    ICommand deferredCommand = _commandAccessor();
                    object deferredParameter = _parameterAccessor();

                    if (deferredCommand?.CanExecute(deferredParameter) == true)
                        deferredCommand.Execute(deferredParameter);
                }, DispatcherPriority.Background);
            }
        }
    }
}
