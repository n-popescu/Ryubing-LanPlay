using Avalonia.Data.Converters;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ryujinx.Ava.UI.Helpers
{
    internal class KeyValueConverter : IValueConverter
    {
        public static readonly KeyValueConverter Instance = new();

        private static readonly Dictionary<Key, LocaleKeys> _keysMap = new()
        {
            { Key.Unknown, LocaleKeys.KeyboardLayout_KeyUnknown },
            { Key.ShiftLeft, LocaleKeys.KeyboardLayout_KeyShiftLeft },
            { Key.ShiftRight, LocaleKeys.KeyboardLayout_KeyShiftRight },
            { Key.ControlLeft, LocaleKeys.KeyboardLayout_KeyControlLeft },
            { Key.ControlRight, LocaleKeys.KeyboardLayout_KeyControlRight },
            { Key.AltLeft, LocaleKeys.KeyboardLayout_KeyAltLeft },
            { Key.AltRight, LocaleKeys.KeyboardLayout_KeyAltRight },
            { Key.WinLeft, LocaleKeys.KeyboardLayout_KeyWinLeft },
            { Key.WinRight, LocaleKeys.KeyboardLayout_KeyWinRight },
            { Key.Up, LocaleKeys.KeyboardLayout_KeyUp },
            { Key.Down, LocaleKeys.KeyboardLayout_KeyDown },
            { Key.Left, LocaleKeys.KeyboardLayout_KeyLeft },
            { Key.Right, LocaleKeys.KeyboardLayout_KeyRight },
            { Key.Enter, LocaleKeys.KeyboardLayout_KeyEnter },
            { Key.Escape, LocaleKeys.KeyboardLayout_KeyEscape },
            { Key.Space, LocaleKeys.KeyboardLayout_KeySpace },
            { Key.Tab, LocaleKeys.KeyboardLayout_KeyTab },
            { Key.BackSpace, LocaleKeys.KeyboardLayout_KeyBackSpace },
            { Key.Insert, LocaleKeys.KeyboardLayout_KeyInsert },
            { Key.Delete, LocaleKeys.KeyboardLayout_KeyDelete },
            { Key.PageUp, LocaleKeys.KeyboardLayout_KeyPageUp },
            { Key.PageDown, LocaleKeys.KeyboardLayout_KeyPageDown },
            { Key.Home, LocaleKeys.KeyboardLayout_KeyHome },
            { Key.End, LocaleKeys.KeyboardLayout_KeyEnd },
            { Key.CapsLock, LocaleKeys.KeyboardLayout_KeyCapsLock },
            { Key.ScrollLock, LocaleKeys.KeyboardLayout_KeyScrollLock },
            { Key.PrintScreen, LocaleKeys.KeyboardLayout_KeyPrintScreen },
            { Key.Pause, LocaleKeys.KeyboardLayout_KeyPause },
            { Key.NumLock, LocaleKeys.KeyboardLayout_KeyNumLock },
            { Key.Clear, LocaleKeys.KeyboardLayout_KeyClear },
            { Key.Keypad0, LocaleKeys.KeyboardLayout_KeyKeypad0 },
            { Key.Keypad1, LocaleKeys.KeyboardLayout_KeyKeypad1 },
            { Key.Keypad2, LocaleKeys.KeyboardLayout_KeyKeypad2 },
            { Key.Keypad3, LocaleKeys.KeyboardLayout_KeyKeypad3 },
            { Key.Keypad4, LocaleKeys.KeyboardLayout_KeyKeypad4 },
            { Key.Keypad5, LocaleKeys.KeyboardLayout_KeyKeypad5 },
            { Key.Keypad6, LocaleKeys.KeyboardLayout_KeyKeypad6 },
            { Key.Keypad7, LocaleKeys.KeyboardLayout_KeyKeypad7 },
            { Key.Keypad8, LocaleKeys.KeyboardLayout_KeyKeypad8 },
            { Key.Keypad9, LocaleKeys.KeyboardLayout_KeyKeypad9 },
            { Key.KeypadDivide, LocaleKeys.KeyboardLayout_KeyKeypadDivide },
            { Key.KeypadMultiply, LocaleKeys.KeyboardLayout_KeyKeypadMultiply },
            { Key.KeypadSubtract, LocaleKeys.KeyboardLayout_KeyKeypadSubtract },
            { Key.KeypadAdd, LocaleKeys.KeyboardLayout_KeyKeypadAdd },
            { Key.KeypadDecimal, LocaleKeys.KeyboardLayout_KeyKeypadDecimal },
            { Key.KeypadEnter, LocaleKeys.KeyboardLayout_KeyKeypadEnter },
            { Key.Number0, LocaleKeys.KeyboardLayout_KeyNumber0 },
            { Key.Number1, LocaleKeys.KeyboardLayout_KeyNumber1 },
            { Key.Number2, LocaleKeys.KeyboardLayout_KeyNumber2 },
            { Key.Number3, LocaleKeys.KeyboardLayout_KeyNumber3 },
            { Key.Number4, LocaleKeys.KeyboardLayout_KeyNumber4 },
            { Key.Number5, LocaleKeys.KeyboardLayout_KeyNumber5 },
            { Key.Number6, LocaleKeys.KeyboardLayout_KeyNumber6 },
            { Key.Number7, LocaleKeys.KeyboardLayout_KeyNumber7 },
            { Key.Number8, LocaleKeys.KeyboardLayout_KeyNumber8 },
            { Key.Number9, LocaleKeys.KeyboardLayout_KeyNumber9 },
            { Key.Tilde, LocaleKeys.KeyboardLayout_KeyTilde },
            { Key.Grave, LocaleKeys.KeyboardLayout_KeyGrave },
            { Key.Minus, LocaleKeys.KeyboardLayout_KeyMinus },
            { Key.Plus, LocaleKeys.KeyboardLayout_KeyPlus },
            { Key.BracketLeft, LocaleKeys.KeyboardLayout_KeyBracketLeft },
            { Key.BracketRight, LocaleKeys.KeyboardLayout_KeyBracketRight },
            { Key.Semicolon, LocaleKeys.KeyboardLayout_KeySemicolon },
            { Key.Quote, LocaleKeys.KeyboardLayout_KeyQuote },
            { Key.Comma, LocaleKeys.KeyboardLayout_KeyComma },
            { Key.Period, LocaleKeys.KeyboardLayout_KeyPeriod },
            { Key.Slash, LocaleKeys.KeyboardLayout_KeySlash },
            { Key.BackSlash, LocaleKeys.KeyboardLayout_KeyBackSlash },
            { Key.Unbound, LocaleKeys.KeyboardLayout_KeyUnbound },
        };

        private static readonly Dictionary<GamepadInputId, LocaleKeys> _gamepadInputIdMap = new()
        {
            { GamepadInputId.LeftStick, LocaleKeys.GamepadLeftStick },
            { GamepadInputId.RightStick, LocaleKeys.GamepadRightStick },
            { GamepadInputId.LeftShoulder, LocaleKeys.GamepadLeftShoulder },
            { GamepadInputId.RightShoulder, LocaleKeys.GamepadRightShoulder },
            { GamepadInputId.LeftTrigger, LocaleKeys.GamepadLeftTrigger },
            { GamepadInputId.RightTrigger, LocaleKeys.GamepadRightTrigger },
            { GamepadInputId.DpadUp, LocaleKeys.GamepadDpadUp},
            { GamepadInputId.DpadDown, LocaleKeys.GamepadDpadDown},
            { GamepadInputId.DpadLeft, LocaleKeys.GamepadDpadLeft},
            { GamepadInputId.DpadRight, LocaleKeys.GamepadDpadRight},
            { GamepadInputId.Minus, LocaleKeys.GamepadMinus},
            { GamepadInputId.Plus, LocaleKeys.GamepadPlus},
            { GamepadInputId.Guide, LocaleKeys.GamepadGuide},
            { GamepadInputId.Misc1, LocaleKeys.GamepadMisc1},
            { GamepadInputId.Paddle1, LocaleKeys.GamepadPaddle1},
            { GamepadInputId.Paddle2, LocaleKeys.GamepadPaddle2},
            { GamepadInputId.Paddle3, LocaleKeys.GamepadPaddle3},
            { GamepadInputId.Paddle4, LocaleKeys.GamepadPaddle4},
            { GamepadInputId.Touchpad, LocaleKeys.GamepadTouchpad},
            { GamepadInputId.SingleLeftTrigger0, LocaleKeys.GamepadSingleLeftTrigger0},
            { GamepadInputId.SingleRightTrigger0, LocaleKeys.GamepadSingleRightTrigger0},
            { GamepadInputId.SingleLeftTrigger1, LocaleKeys.GamepadSingleLeftTrigger1},
            { GamepadInputId.SingleRightTrigger1, LocaleKeys.GamepadSingleRightTrigger1},
            { GamepadInputId.Unbound, LocaleKeys.KeyboardLayout_KeyUnbound},
        };

        private static readonly Dictionary<StickInputId, LocaleKeys> _stickInputIdMap = new()
        {
            { StickInputId.Left, LocaleKeys.StickLeft},
            { StickInputId.Right, LocaleKeys.StickRight},
            { StickInputId.Unbound, LocaleKeys.KeyboardLayout_KeyUnbound},
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string keyString = string.Empty;
            LocaleKeys localeKey;

            switch (value)
            {
                case Key key:
                    if (_keysMap.TryGetValue(key, out localeKey))
                    {
                        if (OperatingSystem.IsMacOS())
                        {
                            localeKey = localeKey switch
                            {
                                LocaleKeys.KeyboardLayout_KeyControlLeft => LocaleKeys.KeyboardLayout_KeyMacControlLeft,
                                LocaleKeys.KeyboardLayout_KeyControlRight => LocaleKeys.KeyboardLayout_KeyMacControlRight,
                                LocaleKeys.KeyboardLayout_KeyAltLeft => LocaleKeys.KeyboardLayout_KeyMacAltLeft,
                                LocaleKeys.KeyboardLayout_KeyAltRight => LocaleKeys.KeyboardLayout_KeyMacAltRight,
                                LocaleKeys.KeyboardLayout_KeyWinLeft => LocaleKeys.KeyboardLayout_KeyMacWinLeft,
                                LocaleKeys.KeyboardLayout_KeyWinRight => LocaleKeys.KeyboardLayout_KeyMacWinRight,
                                _ => localeKey
                            };
                        }

                        keyString = LocaleManager.Instance[localeKey];
                    }
                    else
                    {
                        keyString = key.ToString();
                    }

                    break;
                case GamepadInputId gamepadInputId:
                    if (_gamepadInputIdMap.TryGetValue(gamepadInputId, out localeKey))
                    {
                        keyString = LocaleManager.Instance[localeKey];
                    }
                    else
                    {
                        keyString = gamepadInputId.ToString();
                    }

                    break;
                case StickInputId stickInputId:
                    if (_stickInputIdMap.TryGetValue(stickInputId, out localeKey))
                    {
                        keyString = LocaleManager.Instance[localeKey];
                    }
                    else
                    {
                        keyString = stickInputId.ToString();
                    }

                    break;
            }

            return keyString;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
