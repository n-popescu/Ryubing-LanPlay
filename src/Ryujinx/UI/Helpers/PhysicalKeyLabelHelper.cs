using Avalonia.Input;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Input;
using Ryujinx.Common.Configuration.Hid;
using System;
using System.Collections.Concurrent;
using AvaPhysicalKey = Avalonia.Input.PhysicalKey;
using ConfigPhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;
using InputKey = Ryujinx.Input.Key;

namespace Ryujinx.Ava.UI.Helpers
{
    internal static class PhysicalKeyLabelHelper
    {
        private static readonly ConcurrentDictionary<ConfigPhysicalKey, string> _observedLayoutLabels = new();
        public static event Action LabelsChanged;

        public static string GetDisplayString(ConfigPhysicalKey key)
        {
            if (KeyboardLayoutLocaleHelper.TryGetPhysicalLabel(key, out string localizedLabel))
            {
                return localizedLabel;
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
            if (!TryConvertToConfigPhysicalKey(inputKey, out ConfigPhysicalKey physicalKey) ||
                KeyboardLayoutLocaleHelper.TryGetPhysicalLocaleKey(physicalKey, out _))
            {
                return;
            }

            if (TryNormalizeObservedPrintableLabel(args.KeySymbol, out string label))
            {
                if (_observedLayoutLabels.TryGetValue(physicalKey, out string existingLabel) && existingLabel == label)
                {
                    return;
                }

                _observedLayoutLabels[physicalKey] = label;
                LabelsChanged?.Invoke();
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
    }
}
