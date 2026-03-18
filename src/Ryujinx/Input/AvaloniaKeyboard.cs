using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Keyboard;
using Ryujinx.Common.Logging;
using Ryujinx.Input;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using ConfigPhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;
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

        private class ButtonMappingEntry(GamepadButtonInputId to, Key from)
        {
            public readonly GamepadButtonInputId To = to;
            public readonly Key From = from;
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
                    if (entry.From == Key.Unknown || entry.From == Key.Unbound || entry.To == GamepadButtonInputId.Unbound)
                    {
                        continue;
                    }

                    // NOTE: Do not touch state of the button already pressed.
                    if (!result.IsPressed(entry.To))
                    {
                        result.SetPressed(entry.To, rawState.IsPressed(entry.From));
                    }
                }

                (short leftStickX, short leftStickY) = GetStickValues(ref rawState, _configuration.LeftJoyconStick);
                (short rightStickX, short rightStickY) = GetStickValues(ref rawState, _configuration.RightJoyconStick);

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

#pragma warning disable IDE0055 // Disable formatting
                // Left JoyCon
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftStick,           _configuration.LeftJoyconStick.StickButton.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadUp,              _configuration.LeftJoycon.DpadUp.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadDown,            _configuration.LeftJoycon.DpadDown.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadLeft,            _configuration.LeftJoycon.DpadLeft.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadRight,           _configuration.LeftJoycon.DpadRight.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Minus,               _configuration.LeftJoycon.ButtonMinus.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftShoulder,        _configuration.LeftJoycon.ButtonL.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftTrigger,         _configuration.LeftJoycon.ButtonZl.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleRightTrigger0, _configuration.LeftJoycon.ButtonSr.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleLeftTrigger0,  _configuration.LeftJoycon.ButtonSl.ToInputKey()));

                // Right JoyCon
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightStick,          _configuration.RightJoyconStick.StickButton.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.A,                   _configuration.RightJoycon.ButtonA.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.B,                   _configuration.RightJoycon.ButtonB.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.X,                   _configuration.RightJoycon.ButtonX.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Y,                   _configuration.RightJoycon.ButtonY.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Plus,                _configuration.RightJoycon.ButtonPlus.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightShoulder,       _configuration.RightJoycon.ButtonR.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightTrigger,        _configuration.RightJoycon.ButtonZr.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleRightTrigger1, _configuration.RightJoycon.ButtonSr.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleLeftTrigger1,  _configuration.RightJoycon.ButtonSl.ToInputKey()));
#pragma warning restore IDE0055
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

        private static (short, short) GetStickValues(ref KeyboardStateSnapshot snapshot, JoyconConfigKeyboardStick<ConfigPhysicalKey> stickConfig)
        {
            short stickX = 0;
            short stickY = 0;

            if (snapshot.IsPressed(stickConfig.StickUp.ToInputKey()))
            {
                stickY += 1;
            }

            if (snapshot.IsPressed(stickConfig.StickDown.ToInputKey()))
            {
                stickY -= 1;
            }

            if (snapshot.IsPressed(stickConfig.StickRight.ToInputKey()))
            {
                stickX += 1;
            }

            if (snapshot.IsPressed(stickConfig.StickLeft.ToInputKey()))
            {
                stickX -= 1;
            }

            Vector2 stick = new(stickX, stickY);

            stick = Vector2.Normalize(stick);

            return ((short)(stick.X * short.MaxValue), (short)(stick.Y * short.MaxValue));
        }

        public void Clear()
        {
            _driver?.Clear(_mode);
        }

        public void Dispose() { }
    }
}
