using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using SDL;
using static SDL.SDL3;

namespace Ryujinx.Input.SDL3
{
    internal unsafe class SDL3Pokeball : IGamepad
    {
        private bool HasConfiguration => _configuration != null;

        private readonly record struct ButtonMappingEntry(GamepadButtonInputId To, GamepadButtonInputId From)
        {
            public bool IsValid => To is not GamepadButtonInputId.Unbound && From is not GamepadButtonInputId.Unbound;
        }

        private StandardControllerInputConfig _configuration;

        private readonly Dictionary<GamepadButtonInputId, SDL_GamepadButton> _buttonsDriverMapping = new()
        {
             {GamepadButtonInputId.LeftStick, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK},
             {GamepadButtonInputId.A, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH},
             {GamepadButtonInputId.B, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST},
        };
        
        private readonly Lock _userMappingLock = new();

        private readonly List<ButtonMappingEntry> _buttonsUserMapping;

        private readonly StickInputId[] _stickUserMapping = new StickInputId[(int)StickInputId.Count]
        {
            StickInputId.Unbound, StickInputId.Left, StickInputId.Right,
        };

        public GamepadFeaturesFlag Features { get; }

        private SDL_Gamepad* _gamepadHandle;
        
        public const string Prefix = "Pokéball Plus";

        public SDL3Pokeball(SDL_Gamepad* gamepadHandle, string driverId)
        {
            _gamepadHandle = gamepadHandle;
            _buttonsUserMapping = new List<ButtonMappingEntry>(10);

            Name = SDL_GetGamepadName(_gamepadHandle);
            Id = driverId;
            Features = GetFeaturesFlag();

            // Enable motion tracking
            if ((Features & GamepadFeaturesFlag.Motion) != 0)
            {
                if (!SDL_SetGamepadSensorEnabled(_gamepadHandle, SDL_SensorType.SDL_SENSOR_ACCEL, true))
                {
                    Logger.Error?.Print(LogClass.Hid,
                        $"Could not enable data reporting for SensorType {SDL_SensorType.SDL_SENSOR_ACCEL}.");
                }

                if (!SDL_SetGamepadSensorEnabled(_gamepadHandle, SDL_SensorType.SDL_SENSOR_GYRO, true))
                {
                    Logger.Error?.Print(LogClass.Hid,
                        $"Could not enable data reporting for SensorType {SDL_SensorType.SDL_SENSOR_GYRO}.");
                }
            }
        }

        private GamepadFeaturesFlag GetFeaturesFlag()
        {
            GamepadFeaturesFlag result = GamepadFeaturesFlag.None;

            if (SDL_GamepadHasSensor(_gamepadHandle, SDL_SensorType.SDL_SENSOR_ACCEL) &&
                SDL_GamepadHasSensor(_gamepadHandle, SDL_SensorType.SDL_SENSOR_GYRO))
            {
                result |= GamepadFeaturesFlag.Motion;
            }

            if (SDL_RumbleGamepad(_gamepadHandle, 0, 0, 100))
            {
                result |= GamepadFeaturesFlag.Rumble;
            }

            return result;
        }

        public string Id { get; }
        public string Name { get; }
        public bool IsConnected => SDL_GamepadConnected(_gamepadHandle);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _gamepadHandle != null)
            {
                SDL_CloseGamepad(_gamepadHandle);

                _gamepadHandle = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void SetTriggerThreshold(float triggerThreshold)
        {

        }

        public void Rumble(float lowFrequency, float highFrequency, uint durationMs)
        {
            if ((Features & GamepadFeaturesFlag.Rumble) == 0)
                return;

            ushort lowFrequencyRaw = (ushort)(lowFrequency * ushort.MaxValue);
            ushort highFrequencyRaw = (ushort)(highFrequency * ushort.MaxValue);

            if (durationMs == uint.MaxValue)
            {
                if (!SDL_RumbleGamepad(_gamepadHandle, lowFrequencyRaw, highFrequencyRaw, SDL_HAPTIC_INFINITY))
                    Logger.Error?.Print(LogClass.Hid, "Rumble is not supported on this game controller.");
            }
            else if (durationMs > SDL_HAPTIC_INFINITY)
            {
                Logger.Error?.Print(LogClass.Hid, $"Unsupported rumble duration {durationMs}");
            }
            else
            {
                if (!SDL_RumbleGamepad(_gamepadHandle, lowFrequencyRaw, highFrequencyRaw, durationMs))
                    Logger.Error?.Print(LogClass.Hid, "Rumble is not supported on this game controller.");
            }
        }

        public Vector3 GetMotionData(MotionInputId inputId)
        {
            SDL_SensorType sensorType = inputId switch
            {
                MotionInputId.Accelerometer => SDL_SensorType.SDL_SENSOR_ACCEL,
                MotionInputId.Gyroscope => SDL_SensorType.SDL_SENSOR_GYRO,
                _ => SDL_SensorType.SDL_SENSOR_INVALID
            };

            if ((Features & GamepadFeaturesFlag.Motion) == 0 || sensorType is SDL_SensorType.SDL_SENSOR_INVALID)
                return Vector3.Zero;

            const int ElementCount = 3;

            float[] values = new float[3];

            fixed (float* pValues = &values[0]) {
                if (!SDL_GetGamepadSensorData(_gamepadHandle, sensorType, pValues, ElementCount))
                    return Vector3.Zero;

                Vector3 value = new Vector3(values[2], values[1], -values[0]);

                return inputId switch
                {
                    MotionInputId.Gyroscope => RadToDegree(value),
                    MotionInputId.Accelerometer => GsToMs2(value),
                    _ => value
                };
            }
        }

        private static Vector3 RadToDegree(Vector3 rad) => rad * (180 / MathF.PI);

        private static Vector3 GsToMs2(Vector3 gs) => gs / SDL_STANDARD_GRAVITY;

        public void SetConfiguration(InputConfig configuration)
        {
            lock (_userMappingLock)
            {
                _configuration = (StandardControllerInputConfig)configuration;

                _buttonsUserMapping.Clear();

                // First update sticks
                _stickUserMapping[(int)StickInputId.Left] = (StickInputId)_configuration.LeftJoyconStick.Joystick;

                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftStick, (GamepadButtonInputId)_configuration.LeftJoyconStick.StickButton));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.A, (GamepadButtonInputId)_configuration.RightJoycon.ButtonA));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.B, (GamepadButtonInputId)_configuration.RightJoycon.ButtonB));

                SetTriggerThreshold(_configuration.TriggerThreshold);
            }
        }

        public void SetLed(uint packedRgb)
        {
        }

        public GamepadStateSnapshot GetStateSnapshot()
        {
            return IGamepad.GetStateSnapshot(this);
        }

        public GamepadStateSnapshot GetMappedStateSnapshot()
        {
            GamepadStateSnapshot rawState = GetStateSnapshot();
            GamepadStateSnapshot result = default;

            lock (_userMappingLock)
            {
                if (_buttonsUserMapping.Count == 0)
                    return rawState;

                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                foreach (ButtonMappingEntry entry in _buttonsUserMapping)
                {
                    if (!entry.IsValid)
                        continue;

                    // Do not touch state of button already pressed
                    if (!result.IsPressed(entry.To))
                    {
                        result.SetPressed(entry.To, rawState.IsPressed(entry.From));
                    }
                }

                (float leftStickX, float leftStickY) = rawState.GetStick(_stickUserMapping[(int)StickInputId.Left]);

                result.SetStick(StickInputId.Left, leftStickX, leftStickY);
            }

            return result;
        }

        private static float ConvertRawStickValue(short value)
        {
            const float ConvertRate = 1.0f / (short.MaxValue + 0.5f);

            return value * ConvertRate;
        }

        private JoyconConfigControllerStick<GamepadInputId, Common.Configuration.Hid.Controller.StickInputId>
            GetLogicalJoyStickConfig(StickInputId inputId)
        {
            switch (inputId)
            {
                case StickInputId.Left:
                    if (_configuration.RightJoyconStick.Joystick ==
                        Common.Configuration.Hid.Controller.StickInputId.Left)
                        return _configuration.RightJoyconStick;
                    else
                        return _configuration.LeftJoyconStick;
            }

            return null;
        }

        public (float, float) GetStick(StickInputId inputId)
        {
            if (inputId == StickInputId.Unbound)
                return (0.0f, 0.0f);

            if (inputId == StickInputId.Left)
            {
                return (0.0f, 0.0f);
            }

            (short stickX, short stickY) = GetStickXY();

            float resultX = ConvertRawStickValue(stickX);
            float resultY = -ConvertRawStickValue(stickY);

            if (HasConfiguration)
            {
                JoyconConfigControllerStick<GamepadInputId, Common.Configuration.Hid.Controller.StickInputId> pokeballStickConfig = GetLogicalJoyStickConfig(inputId);

                if (pokeballStickConfig != null)
                {
                    if (pokeballStickConfig.InvertStickX)
                        resultX = -resultX;

                    if (pokeballStickConfig.InvertStickY)
                        resultY = -resultY;

                    if (pokeballStickConfig.Rotate90CW)
                    {
                        float temp = resultX;
                        resultX = resultY;
                        resultY = -temp;
                    }
                }
            }

            return inputId switch
            {
                StickInputId.Left => (resultY, -resultX),
                _ => (0.0f, 0.0f)
            };
        }

        private (short, short) GetStickXY()
        {
            return (
                SDL_GetGamepadAxis(_gamepadHandle, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX),
                SDL_GetGamepadAxis(_gamepadHandle, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY));
        }

        public bool IsPressed(GamepadButtonInputId inputId)
        {
            if (!_buttonsDriverMapping.TryGetValue(inputId, out SDL_GamepadButton button))
            {
                return false;
            }

            return SDL_GetGamepadButton(_gamepadHandle, button);
        }
        
        public static bool IsPokeball(SDL_JoystickID gamepadsId)
        {
            return SDL_GetGamepadNameForID(gamepadsId) is Prefix;
        }
    }
}
