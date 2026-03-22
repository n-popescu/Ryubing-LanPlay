using Ryujinx.Ava.Common.Locale;
using System;
using System.Collections.Generic;
using ConfigPhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;
using InputKey = Ryujinx.Input.Key;

namespace Ryujinx.Ava.UI.Helpers
{
    internal static class KeyboardLayoutLocaleHelper
    {
        private static readonly Dictionary<InputKey, LocaleKeys> _sharedLocalizedKeysMap = new()
        {
            [InputKey.Unknown] = LocaleKeys.KeyboardLayout_Key_Unknown,
            [InputKey.ShiftLeft] = LocaleKeys.KeyboardLayout_Key_ShiftLeft,
            [InputKey.ShiftRight] = LocaleKeys.KeyboardLayout_Key_ShiftRight,
            [InputKey.ControlLeft] = LocaleKeys.KeyboardLayout_Key_ControlLeft,
            [InputKey.ControlRight] = LocaleKeys.KeyboardLayout_Key_ControlRight,
            [InputKey.AltLeft] = LocaleKeys.KeyboardLayout_Key_AltLeft,
            [InputKey.AltRight] = LocaleKeys.KeyboardLayout_Key_AltRight,
            [InputKey.WinLeft] = LocaleKeys.KeyboardLayout_Key_WinLeft,
            [InputKey.WinRight] = LocaleKeys.KeyboardLayout_Key_WinRight,
            [InputKey.Up] = LocaleKeys.KeyboardLayout_Key_Up,
            [InputKey.Down] = LocaleKeys.KeyboardLayout_Key_Down,
            [InputKey.Left] = LocaleKeys.KeyboardLayout_Key_Left,
            [InputKey.Right] = LocaleKeys.KeyboardLayout_Key_Right,
            [InputKey.Enter] = LocaleKeys.KeyboardLayout_Key_Enter,
            [InputKey.Escape] = LocaleKeys.KeyboardLayout_Key_Escape,
            [InputKey.Space] = LocaleKeys.KeyboardLayout_Key_Space,
            [InputKey.Tab] = LocaleKeys.KeyboardLayout_Key_Tab,
            [InputKey.BackSpace] = LocaleKeys.KeyboardLayout_Key_BackSpace,
            [InputKey.Insert] = LocaleKeys.KeyboardLayout_Key_Insert,
            [InputKey.Delete] = LocaleKeys.KeyboardLayout_Key_Delete,
            [InputKey.PageUp] = LocaleKeys.KeyboardLayout_Key_PageUp,
            [InputKey.PageDown] = LocaleKeys.KeyboardLayout_Key_PageDown,
            [InputKey.Home] = LocaleKeys.KeyboardLayout_Key_Home,
            [InputKey.End] = LocaleKeys.KeyboardLayout_Key_End,
            [InputKey.CapsLock] = LocaleKeys.KeyboardLayout_Key_CapsLock,
            [InputKey.ScrollLock] = LocaleKeys.KeyboardLayout_Key_ScrollLock,
            [InputKey.PrintScreen] = LocaleKeys.KeyboardLayout_Key_PrintScreen,
            [InputKey.Pause] = LocaleKeys.KeyboardLayout_Key_Pause,
            [InputKey.NumLock] = LocaleKeys.KeyboardLayout_Key_NumLock,
            [InputKey.Clear] = LocaleKeys.KeyboardLayout_Key_Clear,
            [InputKey.Keypad0] = LocaleKeys.KeyboardLayout_Key_Keypad0,
            [InputKey.Keypad1] = LocaleKeys.KeyboardLayout_Key_Keypad1,
            [InputKey.Keypad2] = LocaleKeys.KeyboardLayout_Key_Keypad2,
            [InputKey.Keypad3] = LocaleKeys.KeyboardLayout_Key_Keypad3,
            [InputKey.Keypad4] = LocaleKeys.KeyboardLayout_Key_Keypad4,
            [InputKey.Keypad5] = LocaleKeys.KeyboardLayout_Key_Keypad5,
            [InputKey.Keypad6] = LocaleKeys.KeyboardLayout_Key_Keypad6,
            [InputKey.Keypad7] = LocaleKeys.KeyboardLayout_Key_Keypad7,
            [InputKey.Keypad8] = LocaleKeys.KeyboardLayout_Key_Keypad8,
            [InputKey.Keypad9] = LocaleKeys.KeyboardLayout_Key_Keypad9,
            [InputKey.KeypadDivide] = LocaleKeys.KeyboardLayout_Key_KeypadDivide,
            [InputKey.KeypadMultiply] = LocaleKeys.KeyboardLayout_Key_KeypadMultiply,
            [InputKey.KeypadSubtract] = LocaleKeys.KeyboardLayout_Key_KeypadSubtract,
            [InputKey.KeypadAdd] = LocaleKeys.KeyboardLayout_Key_KeypadAdd,
            [InputKey.KeypadDecimal] = LocaleKeys.KeyboardLayout_Key_KeypadDecimal,
            [InputKey.KeypadEnter] = LocaleKeys.KeyboardLayout_Key_KeypadEnter,
            [InputKey.Unbound] = LocaleKeys.KeyboardLayout_Key_Unbound,
        };

        private static readonly Dictionary<InputKey, LocaleKeys> _semanticPrintableKeysMap = new()
        {
            [InputKey.Number0] = LocaleKeys.KeyboardLayout_Key_Number0,
            [InputKey.Number1] = LocaleKeys.KeyboardLayout_Key_Number1,
            [InputKey.Number2] = LocaleKeys.KeyboardLayout_Key_Number2,
            [InputKey.Number3] = LocaleKeys.KeyboardLayout_Key_Number3,
            [InputKey.Number4] = LocaleKeys.KeyboardLayout_Key_Number4,
            [InputKey.Number5] = LocaleKeys.KeyboardLayout_Key_Number5,
            [InputKey.Number6] = LocaleKeys.KeyboardLayout_Key_Number6,
            [InputKey.Number7] = LocaleKeys.KeyboardLayout_Key_Number7,
            [InputKey.Number8] = LocaleKeys.KeyboardLayout_Key_Number8,
            [InputKey.Number9] = LocaleKeys.KeyboardLayout_Key_Number9,
            [InputKey.Tilde] = LocaleKeys.KeyboardLayout_Key_Tilde,
            [InputKey.Grave] = LocaleKeys.KeyboardLayout_Key_Grave,
            [InputKey.Minus] = LocaleKeys.KeyboardLayout_Key_Minus,
            [InputKey.Plus] = LocaleKeys.KeyboardLayout_Key_Plus,
            [InputKey.BracketLeft] = LocaleKeys.KeyboardLayout_Key_BracketLeft,
            [InputKey.BracketRight] = LocaleKeys.KeyboardLayout_Key_BracketRight,
            [InputKey.Semicolon] = LocaleKeys.KeyboardLayout_Key_Semicolon,
            [InputKey.Quote] = LocaleKeys.KeyboardLayout_Key_Quote,
            [InputKey.Comma] = LocaleKeys.KeyboardLayout_Key_Comma,
            [InputKey.Period] = LocaleKeys.KeyboardLayout_Key_Period,
            [InputKey.Slash] = LocaleKeys.KeyboardLayout_Key_Slash,
            [InputKey.BackSlash] = LocaleKeys.KeyboardLayout_Key_BackSlash,
        };

        public static bool TryGetSemanticLabel(InputKey key, out string label)
        {
            if (TryGetSemanticLocaleKey(key, out LocaleKeys localeKey))
            {
                label = GetLocalizedString(localeKey);
                return true;
            }

            label = string.Empty;
            return false;
        }

        public static bool TryGetPhysicalLabel(ConfigPhysicalKey key, out string label)
        {
            if (TryGetPhysicalLocaleKey(key, out LocaleKeys localeKey))
            {
                label = GetLocalizedString(localeKey);
                return true;
            }

            label = string.Empty;
            return false;
        }

        public static bool TryGetPhysicalLocaleKey(ConfigPhysicalKey key, out LocaleKeys localeKey)
        {
            return _sharedLocalizedKeysMap.TryGetValue((InputKey)(int)key, out localeKey);
        }

        private static bool TryGetSemanticLocaleKey(InputKey key, out LocaleKeys localeKey)
        {
            return _sharedLocalizedKeysMap.TryGetValue(key, out localeKey) ||
                   _semanticPrintableKeysMap.TryGetValue(key, out localeKey);
        }

        private static string GetLocalizedString(LocaleKeys localeKey)
        {
            if (OperatingSystem.IsMacOS())
            {
                localeKey = localeKey switch
                {
                    LocaleKeys.KeyboardLayout_Key_ControlLeft => LocaleKeys.KeyboardLayout_Key_Mac_ControlLeft,
                    LocaleKeys.KeyboardLayout_Key_ControlRight => LocaleKeys.KeyboardLayout_Key_Mac_ControlRight,
                    LocaleKeys.KeyboardLayout_Key_AltLeft => LocaleKeys.KeyboardLayout_Key_Mac_AltLeft,
                    LocaleKeys.KeyboardLayout_Key_AltRight => LocaleKeys.KeyboardLayout_Key_Mac_AltRight,
                    LocaleKeys.KeyboardLayout_Key_WinLeft => LocaleKeys.KeyboardLayout_Key_Mac_WinLeft,
                    LocaleKeys.KeyboardLayout_Key_WinRight => LocaleKeys.KeyboardLayout_Key_Mac_WinRight,
                    _ => localeKey
                };
            }

            return LocaleManager.Instance[localeKey];
        }
    }
}
