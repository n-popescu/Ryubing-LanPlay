using Avalonia.Controls;
using Avalonia.Input;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Input;
using System;
using System.Collections.Generic;
using ConfigPhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;
using Key = Ryujinx.Input.Key;

namespace Ryujinx.Ava.Input
{
    internal class AvaloniaKeyboardDriver : IKeyboardModeDriver
    {
        private static readonly string[] _keyboardIdentifers = ["0"];
        private readonly Control _control;
        private readonly Dictionary<Key, int> _semanticPressedKeys;
        private readonly Dictionary<ConfigPhysicalKey, int> _physicalPressedKeys;
        private readonly Dictionary<Key, ConfigPhysicalKey> _observedPhysicalKeysBySemanticKey;
        private readonly KeyboardInputMode _defaultMode;

        public event EventHandler<KeyEventArgs> KeyPressed;
        public event EventHandler<KeyEventArgs> KeyRelease;
        public event EventHandler<string> TextInput;

        public string DriverName => "AvaloniaKeyboardDriver";
        public ReadOnlySpan<string> GamepadsIds => _keyboardIdentifers;

        public AvaloniaKeyboardDriver(Control control, KeyboardInputMode defaultMode = KeyboardInputMode.Semantic)
        {
            _control = control;
            _semanticPressedKeys = [];
            _physicalPressedKeys = [];
            _observedPhysicalKeysBySemanticKey = [];
            _defaultMode = defaultMode;

            _control.KeyDown += OnKeyPress;
            _control.KeyUp += OnKeyRelease;
            _control.TextInput += Control_TextInput;
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

            return new AvaloniaKeyboard(this, _keyboardIdentifers[0], LocaleManager.Instance[LocaleKeys.KeyboardLayout_AllKeyboards], mode);
        }

        public IEnumerable<IGamepad> GetGamepads() => [GetGamepad("0")];

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _control.KeyDown -= OnKeyPress;
                _control.KeyUp -= OnKeyRelease;
                _control.TextInput -= Control_TextInput;
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
                ? _physicalPressedKeys.ContainsKey((ConfigPhysicalKey)(int)key)
                : _semanticPressedKeys.ContainsKey(key);
        }

        internal void Clear(KeyboardInputMode mode)
        {
            if (mode == KeyboardInputMode.Physical)
            {
                _physicalPressedKeys.Clear();
            }
            else
            {
                _semanticPressedKeys.Clear();
            }
        }

        public void Clear()
        {
            _semanticPressedKeys.Clear();
            _physicalPressedKeys.Clear();
        }

        private static void UpdateKeyState(Dictionary<Key, int> pressedKeys, Key key, bool isPressed)
        {
            if (key is Key.Unknown or Key.Unbound)
            {
                return;
            }

            if (isPressed)
            {
                if (pressedKeys.TryGetValue(key, out int count))
                {
                    pressedKeys[key] = count + 1;
                }
                else
                {
                    pressedKeys[key] = 1;
                }

                return;
            }

            if (pressedKeys.TryGetValue(key, out int currentCount))
            {
                if (currentCount <= 1)
                {
                    pressedKeys.Remove(key);
                }
                else
                {
                    pressedKeys[key] = currentCount - 1;
                }
            }
        }

        private static void UpdateKeyState(Dictionary<ConfigPhysicalKey, int> pressedKeys, ConfigPhysicalKey key, bool isPressed)
        {
            if (key is ConfigPhysicalKey.Unknown or ConfigPhysicalKey.Unbound)
            {
                return;
            }

            if (isPressed)
            {
                if (pressedKeys.TryGetValue(key, out int count))
                {
                    pressedKeys[key] = count + 1;
                }
                else
                {
                    pressedKeys[key] = 1;
                }

                return;
            }

            if (pressedKeys.TryGetValue(key, out int currentCount))
            {
                if (currentCount <= 1)
                {
                    pressedKeys.Remove(key);
                }
                else
                {
                    pressedKeys[key] = currentCount - 1;
                }
            }
        }

        private void UpdateKeyStates(KeyEventArgs args, bool isPressed)
        {
            Key semanticKey = AvaloniaKeyboardMappingHelper.ToInputKey(args.Key);
            Key resolvedSemanticKey = AvaloniaKeyboardMappingHelper.ToInputKey(args.PhysicalKey, args.Key);
            ConfigPhysicalKey physicalKey = GetPhysicalInputKey(args, semanticKey);

            UpdateKeyState(_semanticPressedKeys, resolvedSemanticKey, isPressed);
            UpdateKeyState(_physicalPressedKeys, physicalKey, isPressed);

            if (isPressed &&
                semanticKey is not Key.Unknown and not Key.Unbound &&
                physicalKey is not ConfigPhysicalKey.Unknown and not ConfigPhysicalKey.Unbound)
            {
                _observedPhysicalKeysBySemanticKey[semanticKey] = physicalKey;
            }
        }

        private ConfigPhysicalKey GetPhysicalInputKey(KeyEventArgs args, Key semanticKey)
        {
            Key key = AvaloniaKeyboardMappingHelper.ToInputKey(args.PhysicalKey);

            if (key is >= Key.Unknown and < Key.Count)
            {
                return (ConfigPhysicalKey)(int)key;
            }

            if (semanticKey is not Key.Unknown and not Key.Unbound &&
                _observedPhysicalKeysBySemanticKey.TryGetValue(semanticKey, out ConfigPhysicalKey observedPhysicalKey))
            {
                return observedPhysicalKey;
            }

            return ConfigPhysicalKey.Unknown;
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
