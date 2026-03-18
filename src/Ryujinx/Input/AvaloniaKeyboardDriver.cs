using Avalonia.Controls;
using Avalonia.Input;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Input;
using System;
using System.Collections.Generic;
using Key = Ryujinx.Input.Key;

namespace Ryujinx.Ava.Input
{
    internal class AvaloniaKeyboardDriver : IGamepadDriver
    {
        private static readonly string[] _keyboardIdentifers = ["0"];
        private readonly Control _control;
        private readonly Dictionary<Key, int> _pressedKeys;

        public event EventHandler<KeyEventArgs> KeyPressed;
        public event EventHandler<KeyEventArgs> KeyRelease;
        public event EventHandler<string> TextInput;

        public string DriverName => "AvaloniaKeyboardDriver";
        public ReadOnlySpan<string> GamepadsIds => _keyboardIdentifers;

        public AvaloniaKeyboardDriver(Control control)
        {
            _control = control;
            _pressedKeys = [];

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
            if (!_keyboardIdentifers[0].Equals(id))
            {
                return null;
            }

            return new AvaloniaKeyboard(this, _keyboardIdentifers[0], LocaleManager.Instance[LocaleKeys.KeyboardLayout_AllKeyboards]);
        }

        public IEnumerable<IGamepad> GetGamepads() => [GetGamepad("0")];

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _control.KeyDown -= OnKeyPress;
                _control.KeyUp -= OnKeyRelease;
            }
        }

        protected void OnKeyPress(object sender, KeyEventArgs args)
        {
            Key key = AvaloniaKeyboardMappingHelper.ToInputKey(args.PhysicalKey, args.Key);

            if (key != Key.Unknown)
            {
                if (_pressedKeys.TryGetValue(key, out int count))
                {
                    _pressedKeys[key] = count + 1;
                }
                else
                {
                    _pressedKeys[key] = 1;
                }
            }

            KeyPressed?.Invoke(this, args);
        }

        protected void OnKeyRelease(object sender, KeyEventArgs args)
        {
            Key key = AvaloniaKeyboardMappingHelper.ToInputKey(args.PhysicalKey, args.Key);

            if (key != Key.Unknown)
            {
                if (_pressedKeys.TryGetValue(key, out int count))
                {
                    if (count <= 1)
                    {
                        _pressedKeys.Remove(key);
                    }
                    else
                    {
                        _pressedKeys[key] = count - 1;
                    }
                }
            }

            KeyRelease?.Invoke(this, args);
        }

        internal bool IsPressed(Key key)
        {
            if (key is Key.Unbound or Key.Unknown)
            {
                return false;
            }

            return _pressedKeys.ContainsKey(key);
        }

        public void Clear()
        {
            _pressedKeys.Clear();
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
