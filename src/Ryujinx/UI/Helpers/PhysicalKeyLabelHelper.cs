using Avalonia.Input;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Input;
using Ryujinx.Common.Configuration.Hid;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AvaPhysicalKey = Avalonia.Input.PhysicalKey;
using ConfigPhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;
using InputKey = Ryujinx.Input.Key;

namespace Ryujinx.Ava.UI.Helpers
{
    internal static class PhysicalKeyLabelHelper
    {
        private static readonly ConcurrentDictionary<ConfigPhysicalKey, string> _observedLayoutLabels = new();

        private static readonly Dictionary<ConfigPhysicalKey, LocaleKeys> _localizedKeysMap = new()
        {
            [ConfigPhysicalKey.Unknown] = LocaleKeys.KeyboardLayout_KeyUnknown,
            [ConfigPhysicalKey.ShiftLeft] = LocaleKeys.KeyboardLayout_KeyShiftLeft,
            [ConfigPhysicalKey.ShiftRight] = LocaleKeys.KeyboardLayout_KeyShiftRight,
            [ConfigPhysicalKey.ControlLeft] = LocaleKeys.KeyboardLayout_KeyControlLeft,
            [ConfigPhysicalKey.ControlRight] = LocaleKeys.KeyboardLayout_KeyControlRight,
            [ConfigPhysicalKey.AltLeft] = LocaleKeys.KeyboardLayout_KeyAltLeft,
            [ConfigPhysicalKey.AltRight] = LocaleKeys.KeyboardLayout_KeyAltRight,
            [ConfigPhysicalKey.WinLeft] = LocaleKeys.KeyboardLayout_KeyWinLeft,
            [ConfigPhysicalKey.WinRight] = LocaleKeys.KeyboardLayout_KeyWinRight,
            [ConfigPhysicalKey.Up] = LocaleKeys.KeyboardLayout_KeyUp,
            [ConfigPhysicalKey.Down] = LocaleKeys.KeyboardLayout_KeyDown,
            [ConfigPhysicalKey.Left] = LocaleKeys.KeyboardLayout_KeyLeft,
            [ConfigPhysicalKey.Right] = LocaleKeys.KeyboardLayout_KeyRight,
            [ConfigPhysicalKey.Enter] = LocaleKeys.KeyboardLayout_KeyEnter,
            [ConfigPhysicalKey.Escape] = LocaleKeys.KeyboardLayout_KeyEscape,
            [ConfigPhysicalKey.Space] = LocaleKeys.KeyboardLayout_KeySpace,
            [ConfigPhysicalKey.Tab] = LocaleKeys.KeyboardLayout_KeyTab,
            [ConfigPhysicalKey.BackSpace] = LocaleKeys.KeyboardLayout_KeyBackSpace,
            [ConfigPhysicalKey.Insert] = LocaleKeys.KeyboardLayout_KeyInsert,
            [ConfigPhysicalKey.Delete] = LocaleKeys.KeyboardLayout_KeyDelete,
            [ConfigPhysicalKey.PageUp] = LocaleKeys.KeyboardLayout_KeyPageUp,
            [ConfigPhysicalKey.PageDown] = LocaleKeys.KeyboardLayout_KeyPageDown,
            [ConfigPhysicalKey.Home] = LocaleKeys.KeyboardLayout_KeyHome,
            [ConfigPhysicalKey.End] = LocaleKeys.KeyboardLayout_KeyEnd,
            [ConfigPhysicalKey.CapsLock] = LocaleKeys.KeyboardLayout_KeyCapsLock,
            [ConfigPhysicalKey.ScrollLock] = LocaleKeys.KeyboardLayout_KeyScrollLock,
            [ConfigPhysicalKey.PrintScreen] = LocaleKeys.KeyboardLayout_KeyPrintScreen,
            [ConfigPhysicalKey.Pause] = LocaleKeys.KeyboardLayout_KeyPause,
            [ConfigPhysicalKey.NumLock] = LocaleKeys.KeyboardLayout_KeyNumLock,
            [ConfigPhysicalKey.Clear] = LocaleKeys.KeyboardLayout_KeyClear,
            [ConfigPhysicalKey.Keypad0] = LocaleKeys.KeyboardLayout_KeyKeypad0,
            [ConfigPhysicalKey.Keypad1] = LocaleKeys.KeyboardLayout_KeyKeypad1,
            [ConfigPhysicalKey.Keypad2] = LocaleKeys.KeyboardLayout_KeyKeypad2,
            [ConfigPhysicalKey.Keypad3] = LocaleKeys.KeyboardLayout_KeyKeypad3,
            [ConfigPhysicalKey.Keypad4] = LocaleKeys.KeyboardLayout_KeyKeypad4,
            [ConfigPhysicalKey.Keypad5] = LocaleKeys.KeyboardLayout_KeyKeypad5,
            [ConfigPhysicalKey.Keypad6] = LocaleKeys.KeyboardLayout_KeyKeypad6,
            [ConfigPhysicalKey.Keypad7] = LocaleKeys.KeyboardLayout_KeyKeypad7,
            [ConfigPhysicalKey.Keypad8] = LocaleKeys.KeyboardLayout_KeyKeypad8,
            [ConfigPhysicalKey.Keypad9] = LocaleKeys.KeyboardLayout_KeyKeypad9,
            [ConfigPhysicalKey.KeypadDivide] = LocaleKeys.KeyboardLayout_KeyKeypadDivide,
            [ConfigPhysicalKey.KeypadMultiply] = LocaleKeys.KeyboardLayout_KeyKeypadMultiply,
            [ConfigPhysicalKey.KeypadSubtract] = LocaleKeys.KeyboardLayout_KeyKeypadSubtract,
            [ConfigPhysicalKey.KeypadAdd] = LocaleKeys.KeyboardLayout_KeyKeypadAdd,
            [ConfigPhysicalKey.KeypadDecimal] = LocaleKeys.KeyboardLayout_KeyKeypadDecimal,
            [ConfigPhysicalKey.KeypadEnter] = LocaleKeys.KeyboardLayout_KeyKeypadEnter,
            [ConfigPhysicalKey.Unbound] = LocaleKeys.KeyboardLayout_KeyUnbound,
        };

        public static string GetDisplayString(ConfigPhysicalKey key)
        {
            if (_localizedKeysMap.TryGetValue(key, out LocaleKeys localeKey))
            {
                return GetLocalizedString(localeKey);
            }

            if (_observedLayoutLabels.TryGetValue(key, out string observedLabel))
            {
                return observedLabel;
            }

            if (TryGetFallbackPrintableKeyLabel(key, out string label))
            {
                return label;
            }

            return key.ToString();
        }

        public static void ObserveKeyPress(object sender, KeyEventArgs args)
        {
            if (args.KeyModifiers != KeyModifiers.None)
            {
                return;
            }

            InputKey inputKey = AvaloniaKeyboardMappingHelper.ToInputKey(args.PhysicalKey);
            if (!TryConvertToConfigPhysicalKey(inputKey, out ConfigPhysicalKey physicalKey) || _localizedKeysMap.ContainsKey(physicalKey))
            {
                return;
            }

            if (TryNormalizeObservedPrintableLabel(args.KeySymbol, out string label))
            {
                _observedLayoutLabels[physicalKey] = label;
            }
        }

        private static bool TryGetFallbackPrintableKeyLabel(ConfigPhysicalKey key, out string label)
        {
            // The legacy enum name for the ISO extra key is misleading, so give it a distinct physical label.
            if (key == ConfigPhysicalKey.Grave)
            {
                label = "<>";
                return true;
            }

            if (!AvaloniaKeyboardMappingHelper.TryGetAvaPhysicalKey((InputKey)(int)key, out AvaPhysicalKey avaPhysicalKey))
            {
                label = string.Empty;
                return false;
            }

            label = PhysicalKeyExtensions.ToQwertyKeySymbol(avaPhysicalKey, false);

            if (string.IsNullOrEmpty(label) || label.Length != 1 || char.IsControl(label[0]))
            {
                label = string.Empty;
                return false;
            }

            if (char.IsLetter(label[0]))
            {
                label = char.ToUpperInvariant(label[0]).ToString();
            }

            return true;
        }

        private static bool TryNormalizeObservedPrintableLabel(string keySymbol, out string label)
        {
            if (string.IsNullOrEmpty(keySymbol) || keySymbol.Length != 1 || char.IsControl(keySymbol[0]))
            {
                label = string.Empty;
                return false;
            }

            label = char.IsLetter(keySymbol[0])
                ? char.ToUpperInvariant(keySymbol[0]).ToString()
                : keySymbol;

            return true;
        }

        private static bool TryConvertToConfigPhysicalKey(InputKey key, out ConfigPhysicalKey physicalKey)
        {
            if (key is >= InputKey.Unknown and < InputKey.Count)
            {
                physicalKey = (ConfigPhysicalKey)(int)key;
                return true;
            }

            physicalKey = ConfigPhysicalKey.Unknown;
            return false;
        }

        private static string GetLocalizedString(LocaleKeys localeKey)
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

            return LocaleManager.Instance[localeKey];
        }
    }
}
