using Avalonia.Controls;
using Avalonia.Input;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Input;
using System;
using System.Collections.Generic;
using Key = Ryujinx.Input.Key;

namespace Ryujinx.Ava.Input
{
    internal class AvaloniaKeyboardDriver : IKeyboardModeDriver
    {
        private static readonly string[] _keyboardIdentifers = ["0"];
        private readonly Control _control;
        private readonly Dictionary<Key, int> _semanticPressedKeys;
        private readonly Dictionary<Key, int> _physicalPressedKeys;
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
            UpdateKeyState(_semanticPressedKeys, GetInputKey(args, KeyboardInputMode.Semantic), true);
            UpdateKeyState(_physicalPressedKeys, GetInputKey(args, KeyboardInputMode.Physical), true);

            KeyPressed?.Invoke(this, args);
        }

        protected void OnKeyRelease(object sender, KeyEventArgs args)
        {
            UpdateKeyState(_semanticPressedKeys, GetInputKey(args, KeyboardInputMode.Semantic), false);
            UpdateKeyState(_physicalPressedKeys, GetInputKey(args, KeyboardInputMode.Physical), false);

            KeyRelease?.Invoke(this, args);
        }

        internal bool IsPressed(Key key, KeyboardInputMode mode)
        {
            if (key is Key.Unbound or Key.Unknown)
            {
                return false;
            }

            return GetPressedKeys(mode).ContainsKey(key);
        }

        internal void Clear(KeyboardInputMode mode)
        {
            GetPressedKeys(mode).Clear();
        }

        public void Clear()
        {
            _semanticPressedKeys.Clear();
            _physicalPressedKeys.Clear();
        }

        private Dictionary<Key, int> GetPressedKeys(KeyboardInputMode mode)
        {
            return mode == KeyboardInputMode.Physical ? _physicalPressedKeys : _semanticPressedKeys;
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

        private static Key GetInputKey(KeyEventArgs args, KeyboardInputMode mode)
        {
            if (mode == KeyboardInputMode.Physical)
            {
                Key physicalKey = AvaloniaKeyboardMappingHelper.ToInputKey(args.PhysicalKey);

                return physicalKey != Key.Unknown
                    ? physicalKey
                    : AvaloniaKeyboardMappingHelper.ToInputKey(args.Key);
            }

            return AvaloniaKeyboardMappingHelper.ToInputKey(args.PhysicalKey, args.Key);
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
