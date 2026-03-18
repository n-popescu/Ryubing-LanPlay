using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Keyboard;
using Ryujinx.Common.Logging;
using Ryujinx.Input;
using System;
using System.Collections.Generic;
using System.Threading;
using Key = Ryujinx.Input.Key;

namespace Ryujinx.Ava.Input
{
    internal class AvaloniaKeyboard : IKeyboard
    {
        private readonly List<ButtonMappingEntry> _buttonsUserMapping;
        private readonly AvaloniaKeyboardDriver _driver;
        private readonly KeyboardInputMode _mode;
        private StandardKeyboardInputConfig _configuration;
        private uint _ledValue;

        private readonly Lock _userMappingLock = new();

        public string Id { get; }
        public string Name { get; }

        public bool IsConnected => true;
        public GamepadFeaturesFlag Features => GamepadFeaturesFlag.None;

        private readonly record struct ButtonMappingEntry(GamepadButtonInputId To, Key From)
        {
            public bool IsValid => To is not GamepadButtonInputId.Unbound && From is not Key.Unknown and not Key.Unbound;
        }

        public AvaloniaKeyboard(AvaloniaKeyboardDriver driver, string id, string name, KeyboardInputMode mode)
        {
            _buttonsUserMapping = [];

            _driver = driver;
            _mode = mode;
            Id = id;
            Name = name;
        }

        public KeyboardStateSnapshot GetKeyboardStateSnapshot()
        {
            return IKeyboard.GetStateSnapshot(this);
        }

        public GamepadStateSnapshot GetMappedStateSnapshot()
        {
            KeyboardStateSnapshot rawState = GetKeyboardStateSnapshot();
            GamepadStateSnapshot result = default;

            lock (_userMappingLock)
            {
                if (_configuration == null)
                {
                    return result;
                }

                foreach (ButtonMappingEntry entry in _buttonsUserMapping)
                {
                    if (!entry.IsValid)
                    {
                        continue;
                    }

                    // NOTE: Do not touch state of the button already pressed.
                    if (!result.IsPressed(entry.To))
                    {
                        result.SetPressed(entry.To, rawState.IsPressed(entry.From));
                    }
                }

                (short leftStickX, short leftStickY) = KeyboardInputMappingHelper.GetStickValues(ref rawState, _configuration.LeftJoyconStick);
                (short rightStickX, short rightStickY) = KeyboardInputMappingHelper.GetStickValues(ref rawState, _configuration.RightJoyconStick);

                result.SetStick(StickInputId.Left, ConvertRawStickValue(leftStickX), ConvertRawStickValue(leftStickY));
                result.SetStick(StickInputId.Right, ConvertRawStickValue(rightStickX), ConvertRawStickValue(rightStickY));
            }

            return result;
        }

        public GamepadStateSnapshot GetStateSnapshot()
        {
            throw new NotSupportedException();
        }

        public (float, float) GetStick(StickInputId inputId)
        {
            throw new NotSupportedException();
        }

        public bool IsPressed(GamepadButtonInputId inputId)
        {
            throw new NotSupportedException();
        }

        public bool IsPressed(Key key)
        {
            try
            {
                return _driver.IsPressed(key, _mode);
            }
            catch
            {
                return false;
            }
        }

        public void SetConfiguration(InputConfig configuration)
        {
            lock (_userMappingLock)
            {
                _configuration = (StandardKeyboardInputConfig)configuration;

                _buttonsUserMapping.Clear();

                foreach (KeyboardInputMappingHelper.KeyboardButtonMapping mapping in KeyboardInputMappingHelper.BuildButtonMappings(_configuration))
                {
                    _buttonsUserMapping.Add(new ButtonMappingEntry(mapping.To, mapping.From));
                }
            }
        }

        public void SetLed(uint packedRgb)
        {
            if (_ledValue == packedRgb)
            {
                return;
            }

            _ledValue = packedRgb;

            Logger.Info?.Print(LogClass.UI, "SetLed called on an AvaloniaKeyboard");

            // Keyboard LED is not supported by this backend.
        }

        public void SetTriggerThreshold(float triggerThreshold) { }

        public void Rumble(float lowFrequency, float highFrequency, uint durationMs) { }

        public Vector3 GetMotionData(MotionInputId inputId) => Vector3.Zero;

        private static float ConvertRawStickValue(short value)
        {
            const float ConvertRate = 1.0f / (short.MaxValue + 0.5f);

            return value * ConvertRate;
        }

        public void Clear()
        {
            _driver?.Clear(_mode);
        }

        public void Dispose() { }
    }
}
