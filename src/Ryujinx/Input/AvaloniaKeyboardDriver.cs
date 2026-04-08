using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Systems.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Input;
using System;
using System.Collections.Generic;
using System.Threading;
using ConfigPhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;
using Key = Ryujinx.Input.Key;

namespace Ryujinx.Ava.Input
{
    internal class AvaloniaKeyboardDriver : IKeyboardModeDriver
    {
        private enum PhysicalKeySource
        {
            Direct,
            ObservedFallback,
            Unknown,
        }

        private static readonly string[] _keyboardIdentifers = ["0"];
        private readonly Control _control;
        private readonly Window _window;
        private readonly HashSet<Key> _semanticPressedKeys;
        private readonly HashSet<ConfigPhysicalKey> _physicalPressedKeys;
        private readonly Dictionary<Key, ConfigPhysicalKey> _observedPhysicalKeysBySemanticKey;
        private readonly Queue<Key> _semanticPressedKeyQueue;
        private readonly Queue<Key> _physicalPressedKeyQueue;
        private readonly Lock _pressedKeyQueueLock;
        private readonly KeyboardInputMode _defaultMode;

        public event EventHandler<KeyEventArgs> KeyPressed;
        public event EventHandler<KeyEventArgs> KeyRelease;
        public event EventHandler<string> TextInput;

        public string DriverName => "AvaloniaKeyboardDriver";
        public ReadOnlySpan<string> GamepadsIds => _keyboardIdentifers;

        public AvaloniaKeyboardDriver(Control control, KeyboardInputMode defaultMode = KeyboardInputMode.Semantic)
        {
            _control = control;
            _window = control as Window ?? TopLevel.GetTopLevel(control) as Window;
            _semanticPressedKeys = [];
            _physicalPressedKeys = [];
            _observedPhysicalKeysBySemanticKey = [];
            _semanticPressedKeyQueue = [];
            _physicalPressedKeyQueue = [];
            _pressedKeyQueueLock = new();
            _defaultMode = defaultMode;

            // Use routed handlers so keys consumed earlier in the Avalonia pipeline
            // can still be observed by the input driver. This is needed for keys like
            // Caps Lock on macOS, which may not reach the plain CLR event path.
            _control.AddHandler(InputElement.KeyDownEvent, OnKeyPress, RoutingStrategies.Tunnel, true);
            _control.AddHandler(InputElement.KeyUpEvent, OnKeyRelease, RoutingStrategies.Tunnel, true);
            _control.TextInput += Control_TextInput;
            _window?.Deactivated += Window_Deactivated;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Clear();
        }

        private void Control_TextInput(object sender, TextInputEventArgs e)
        {
            TextInput?.Invoke(this, e.Text);
        }

        public event Action<string> OnGamepadConnected
        {
            add { }
            remove { }
        }

        public event Action<string> OnGamepadDisconnected
        {
            add { }
            remove { }
        }

        public IGamepad GetGamepad(string id)
        {
            return GetKeyboard(id, _defaultMode);
        }

        public IKeyboard GetKeyboard(string id, KeyboardInputMode mode)
        {
            if (!_keyboardIdentifers[0].Equals(id))
            {
                return null;
            }

            return new AvaloniaKeyboard(this, _keyboardIdentifers[0], LocaleManager.Instance[LocaleKeys.KeyboardLayout_KeyboardInputMode], mode);
        }

        public IEnumerable<IGamepad> GetGamepads() => [GetGamepad("0")];

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _control.RemoveHandler(InputElement.KeyDownEvent, OnKeyPress);
                _control.RemoveHandler(InputElement.KeyUpEvent, OnKeyRelease);
                _control.TextInput -= Control_TextInput;
                if (_window != null)
                {
                    _window.Deactivated -= Window_Deactivated;
                }
            }
        }

        protected void OnKeyPress(object sender, KeyEventArgs args)
        {
            UpdateKeyStates(args, isPressed: true);
            KeyPressed?.Invoke(this, args);
        }

        protected void OnKeyRelease(object sender, KeyEventArgs args)
        {
            UpdateKeyStates(args, isPressed: false);
            KeyRelease?.Invoke(this, args);
        }

        internal bool IsPressed(Key key, KeyboardInputMode mode)
        {
            if (key is Key.Unbound or Key.Unknown)
            {
                return false;
            }

            return mode == KeyboardInputMode.Physical
                ? _physicalPressedKeys.Contains((ConfigPhysicalKey)(int)key)
                : _semanticPressedKeys.Contains(key);
        }

        internal void Clear(KeyboardInputMode mode)
        {
            lock (_pressedKeyQueueLock)
            {
                if (mode == KeyboardInputMode.Physical)
                {
                    _physicalPressedKeys.Clear();
                    _physicalPressedKeyQueue.Clear();
                }
                else
                {
                    _semanticPressedKeys.Clear();
                    _semanticPressedKeyQueue.Clear();
                }
            }
        }

        public void Clear()
        {
            lock (_pressedKeyQueueLock)
            {
                _semanticPressedKeys.Clear();
                _physicalPressedKeys.Clear();
                _semanticPressedKeyQueue.Clear();
                _physicalPressedKeyQueue.Clear();
            }
        }

        internal bool TryConsumePressedKey(KeyboardInputMode mode, out Key key)
        {
            lock (_pressedKeyQueueLock)
            {
                Queue<Key> queue = mode == KeyboardInputMode.Physical ? _physicalPressedKeyQueue : _semanticPressedKeyQueue;

                if (queue.TryDequeue(out key))
                {
                    return true;
                }
            }

            key = Key.Unknown;
            return false;
        }

        private static void UpdateKeyState(HashSet<Key> pressedKeys, Key key, bool isPressed)
        {
            if (key is Key.Unknown or Key.Unbound)
            {
                return;
            }

            if (isPressed)
            {
                pressedKeys.Add(key);
                return;
            }

            pressedKeys.Remove(key);
        }

        private static void UpdateKeyState(HashSet<ConfigPhysicalKey> pressedKeys, ConfigPhysicalKey key, bool isPressed)
        {
            if (key is ConfigPhysicalKey.Unknown or ConfigPhysicalKey.Unbound)
            {
                return;
            }

            if (isPressed)
            {
                pressedKeys.Add(key);
                return;
            }

            pressedKeys.Remove(key);
        }

        private void UpdateKeyStates(KeyEventArgs args, bool isPressed)
        {
            Key semanticKey = AvaloniaKeyboardMappingHelper.ToInputKey(args.Key);
            Key resolvedSemanticKey = AvaloniaKeyboardMappingHelper.ToInputKey(args.PhysicalKey, args.Key);
            ConfigPhysicalKey physicalKey = GetPhysicalInputKey(args, semanticKey, out PhysicalKeySource physicalKeySource);
            bool semanticWasPressed = _semanticPressedKeys.Contains(resolvedSemanticKey);
            bool physicalWasPressed = _physicalPressedKeys.Contains(physicalKey);
            bool semanticStateChanged = resolvedSemanticKey is not Key.Unknown and not Key.Unbound && semanticWasPressed != isPressed;
            bool physicalStateChanged = physicalKey is not ConfigPhysicalKey.Unknown and not ConfigPhysicalKey.Unbound && physicalWasPressed != isPressed;
            bool bufferedSemanticPress = false;
            bool bufferedPhysicalPress = false;

            UpdateKeyState(_semanticPressedKeys, resolvedSemanticKey, isPressed);
            UpdateKeyState(_physicalPressedKeys, physicalKey, isPressed);

            if (isPressed)
            {
                lock (_pressedKeyQueueLock)
                {
                    if (!semanticWasPressed && resolvedSemanticKey is not Key.Unknown and not Key.Unbound)
                    {
                        _semanticPressedKeyQueue.Enqueue(resolvedSemanticKey);
                        bufferedSemanticPress = true;
                    }

                    if (!physicalWasPressed && physicalKey is not ConfigPhysicalKey.Unknown and not ConfigPhysicalKey.Unbound)
                    {
                        _physicalPressedKeyQueue.Enqueue((Key)(int)physicalKey);
                        bufferedPhysicalPress = true;
                    }
                }
            }

            if (isPressed &&
                semanticKey is not Key.Unknown and not Key.Unbound &&
                physicalKey is not ConfigPhysicalKey.Unknown and not ConfigPhysicalKey.Unbound)
            {
                _observedPhysicalKeysBySemanticKey[semanticKey] = physicalKey;
            }

            if (ConfigurationState.Instance.Logger.EnableAvaloniaLog &&
                (semanticStateChanged || physicalStateChanged))
            {
                Logger.Info?.Print(
                    LogClass.UI,
                    $"Keyboard {(isPressed ? "down" : "up")}: avaloniaKey={args.Key}, avaloniaPhysical={args.PhysicalKey}, keySymbol={FormatKeySymbol(args.KeySymbol)}, modifiers={args.KeyModifiers}, semantic={semanticKey}, resolvedSemantic={resolvedSemanticKey}, physical={physicalKey}, physicalSource={physicalKeySource}, bufferedSemantic={bufferedSemanticPress}, bufferedPhysical={bufferedPhysicalPress}, semanticPressed={_semanticPressedKeys.Count}, physicalPressed={_physicalPressedKeys.Count}");
            }
        }

        private ConfigPhysicalKey GetPhysicalInputKey(KeyEventArgs args, Key semanticKey, out PhysicalKeySource source)
        {
            Key key = AvaloniaKeyboardMappingHelper.ToInputKey(args.PhysicalKey);

            if (key is >= Key.Unknown and < Key.Count)
            {
                source = PhysicalKeySource.Direct;
                return (ConfigPhysicalKey)(int)key;
            }

            if (semanticKey is not Key.Unknown and not Key.Unbound &&
                _observedPhysicalKeysBySemanticKey.TryGetValue(semanticKey, out ConfigPhysicalKey observedPhysicalKey))
            {
                source = PhysicalKeySource.ObservedFallback;
                return observedPhysicalKey;
            }

            source = PhysicalKeySource.Unknown;
            return ConfigPhysicalKey.Unknown;
        }

        private static string FormatKeySymbol(string keySymbol)
        {
            return string.IsNullOrEmpty(keySymbol) ? "<none>" : keySymbol;
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
