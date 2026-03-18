using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Keyboard;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Hid;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using SDL;
using static SDL.SDL3;

using ConfigPhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;

namespace Ryujinx.Input.SDL3
{
    class SDL3Keyboard : IKeyboard
    {
        private readonly record struct ButtonMappingEntry(GamepadButtonInputId To, Key From)
        {
            public bool IsValid => To is not GamepadButtonInputId.Unbound && From is not Key.Unbound;
        }

        private readonly Lock _userMappingLock = new();

#pragma warning disable IDE0052 // Remove unread private member
        private readonly SDL3KeyboardDriver _driver;
#pragma warning restore IDE0052
        private StandardKeyboardInputConfig _configuration;
        private readonly List<ButtonMappingEntry> _buttonsUserMapping;


        private static readonly SDL_Keycode[] _keysDriverMapping =
        [
            // INVALID
            SDL_Keycode.SDLK_0,
            // Presented as modifiers, so invalid here.
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,

            SDL_Keycode.SDLK_F1,
            SDL_Keycode.SDLK_F2,
            SDL_Keycode.SDLK_F3,
            SDL_Keycode.SDLK_F4,
            SDL_Keycode.SDLK_F5,
            SDL_Keycode.SDLK_F6,
            SDL_Keycode.SDLK_F7,
            SDL_Keycode.SDLK_F8,
            SDL_Keycode.SDLK_F9,
            SDL_Keycode.SDLK_F10,
            SDL_Keycode.SDLK_F11,
            SDL_Keycode.SDLK_F12,
            SDL_Keycode.SDLK_F13,
            SDL_Keycode.SDLK_F14,
            SDL_Keycode.SDLK_F15,
            SDL_Keycode.SDLK_F16,
            SDL_Keycode.SDLK_F17,
            SDL_Keycode.SDLK_F18,
            SDL_Keycode.SDLK_F19,
            SDL_Keycode.SDLK_F20,
            SDL_Keycode.SDLK_F21,
            SDL_Keycode.SDLK_F22,
            SDL_Keycode.SDLK_F23,
            SDL_Keycode.SDLK_F24,

            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,

            SDL_Keycode.SDLK_UP,
            SDL_Keycode.SDLK_DOWN,
            SDL_Keycode.SDLK_LEFT,
            SDL_Keycode.SDLK_RIGHT,
            SDL_Keycode.SDLK_RETURN,
            SDL_Keycode.SDLK_ESCAPE,
            SDL_Keycode.SDLK_SPACE,
            SDL_Keycode.SDLK_TAB,
            SDL_Keycode.SDLK_BACKSPACE,
            SDL_Keycode.SDLK_INSERT,
            SDL_Keycode.SDLK_DELETE,
            SDL_Keycode.SDLK_PAGEUP,
            SDL_Keycode.SDLK_PAGEDOWN,
            SDL_Keycode.SDLK_HOME,
            SDL_Keycode.SDLK_END,
            SDL_Keycode.SDLK_CAPSLOCK,
            SDL_Keycode.SDLK_SCROLLLOCK,
            SDL_Keycode.SDLK_PRINTSCREEN,
            SDL_Keycode.SDLK_PAUSE,
            SDL_Keycode.SDLK_NUMLOCKCLEAR,
            SDL_Keycode.SDLK_CLEAR,
            SDL_Keycode.SDLK_KP_0,
            SDL_Keycode.SDLK_KP_1,
            SDL_Keycode.SDLK_KP_2,
            SDL_Keycode.SDLK_KP_3,
            SDL_Keycode.SDLK_KP_4,
            SDL_Keycode.SDLK_KP_5,
            SDL_Keycode.SDLK_KP_6,
            SDL_Keycode.SDLK_KP_7,
            SDL_Keycode.SDLK_KP_8,
            SDL_Keycode.SDLK_KP_9,
            SDL_Keycode.SDLK_KP_DIVIDE,
            SDL_Keycode.SDLK_KP_MULTIPLY,
            SDL_Keycode.SDLK_KP_MINUS,
            SDL_Keycode.SDLK_KP_PLUS,
            SDL_Keycode.SDLK_KP_DECIMAL,
            SDL_Keycode.SDLK_KP_ENTER,
            SDL_Keycode.SDLK_A,
            SDL_Keycode.SDLK_B,
            SDL_Keycode.SDLK_C,
            SDL_Keycode.SDLK_D,
            SDL_Keycode.SDLK_E,
            SDL_Keycode.SDLK_F,
            SDL_Keycode.SDLK_G,
            SDL_Keycode.SDLK_H,
            SDL_Keycode.SDLK_I,
            SDL_Keycode.SDLK_J,
            SDL_Keycode.SDLK_K,
            SDL_Keycode.SDLK_L,
            SDL_Keycode.SDLK_M,
            SDL_Keycode.SDLK_N,
            SDL_Keycode.SDLK_O,
            SDL_Keycode.SDLK_P,
            SDL_Keycode.SDLK_Q,
            SDL_Keycode.SDLK_R,
            SDL_Keycode.SDLK_S,
            SDL_Keycode.SDLK_T,
            SDL_Keycode.SDLK_U,
            SDL_Keycode.SDLK_V,
            SDL_Keycode.SDLK_W,
            SDL_Keycode.SDLK_X,
            SDL_Keycode.SDLK_Y,
            SDL_Keycode.SDLK_Z,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_1,
            SDL_Keycode.SDLK_2,
            SDL_Keycode.SDLK_3,
            SDL_Keycode.SDLK_4,
            SDL_Keycode.SDLK_5,
            SDL_Keycode.SDLK_6,
            SDL_Keycode.SDLK_7,
            SDL_Keycode.SDLK_8,
            SDL_Keycode.SDLK_9,
            SDL_Keycode.SDLK_GRAVE,
            SDL_Keycode.SDLK_GRAVE,
            SDL_Keycode.SDLK_MINUS,
            SDL_Keycode.SDLK_PLUS,
            SDL_Keycode.SDLK_LEFTBRACKET,
            SDL_Keycode.SDLK_RIGHTBRACKET,
            SDL_Keycode.SDLK_SEMICOLON,
            SDL_Keycode.SDLK_APOSTROPHE,
            SDL_Keycode.SDLK_COMMA,
            SDL_Keycode.SDLK_PERIOD,
            SDL_Keycode.SDLK_SLASH,
            SDL_Keycode.SDLK_BACKSLASH,

            // Invalids
            SDL_Keycode.SDLK_0
        ];

        public SDL3Keyboard(SDL3KeyboardDriver driver, string id, string name)
        {
            _driver = driver;
            Id = id;
            Name = name;
            _buttonsUserMapping = [];
        }

        private bool HasConfiguration => _configuration != null;

        public string Id { get; }

        public string Name { get; }

        public bool IsConnected => true;

        public GamepadFeaturesFlag Features => GamepadFeaturesFlag.None;

        public void Dispose()
        {
            // No operations
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static int ToSDL3Scancode(Key key)
        {
            if (key is >= Key.Unknown and <= Key.Menu)
            {
                return -1;
            }

            return (int)SDL_GetScancodeFromKey(_keysDriverMapping[(int)key], null);
        }

        private static SDL_Keymod GetKeyboardModifierMask(Key key)
        {
            return key switch
            {
                Key.ShiftLeft => SDL_Keymod.SDL_KMOD_LSHIFT,
                Key.ShiftRight => SDL_Keymod.SDL_KMOD_RSHIFT,
                Key.ControlLeft => SDL_Keymod.SDL_KMOD_LCTRL,
                Key.ControlRight => SDL_Keymod.SDL_KMOD_RCTRL,
                Key.AltLeft => SDL_Keymod.SDL_KMOD_LALT,
                Key.AltRight => SDL_Keymod.SDL_KMOD_RALT,
                Key.WinLeft => SDL_Keymod.SDL_KMOD_LGUI,
                Key.WinRight => SDL_Keymod.SDL_KMOD_RGUI,
                // NOTE: Menu key isn't supported by SDL3.
                _ => SDL_Keymod.SDL_KMOD_NONE
            };
        }

        public unsafe KeyboardStateSnapshot GetKeyboardStateSnapshot()
        {
            SDLBool* rawKeyboardState;
            SDL_Keymod rawKeyboardModifierState = SDL_GetModState();

            unsafe
            {
                rawKeyboardState = SDL_GetKeyboardState(null);
            }

            bool[] keysState = new bool[(int)Key.Count];

            for (Key key = 0; key < Key.Count; key++)
            {
                int index = ToSDL3Scancode(key);
                if (index == -1)
                {
                    SDL_Keymod modifierMask = GetKeyboardModifierMask(key);

                    if (modifierMask == SDL_Keymod.SDL_KMOD_NONE)
                    {
                        continue;
                    }

                    keysState[(int)key] = (rawKeyboardModifierState & modifierMask) == modifierMask;
                }
                else
                {
                    keysState[(int)key] = rawKeyboardState[index];
                }
            }

            return new KeyboardStateSnapshot(keysState);
        }

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

            Vector2 stick = Vector2.Normalize(new Vector2(stickX, stickY));

            return ((short)(stick.X * short.MaxValue), (short)(stick.Y * short.MaxValue));
        }

        public GamepadStateSnapshot GetMappedStateSnapshot()
        {
            KeyboardStateSnapshot rawState = GetKeyboardStateSnapshot();
            GamepadStateSnapshot result = default;

            lock (_userMappingLock)
            {
                if (!HasConfiguration)
                {
                    return result;
                }

                foreach (ButtonMappingEntry entry in _buttonsUserMapping)
                {
                    if (entry.From == Key.Unknown || entry.From == Key.Unbound || entry.To == GamepadButtonInputId.Unbound)
                    {
                        continue;
                    }

                    // Do not touch state of button already pressed
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
            // We only implement GetKeyboardStateSnapshot.
            throw new NotSupportedException();
        }

        public void SetConfiguration(InputConfig configuration)
        {
            lock (_userMappingLock)
            {
                _configuration = (StandardKeyboardInputConfig)configuration;

                // First clear the buttons mapping
                _buttonsUserMapping.Clear();

                // Then configure left joycon
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftStick, _configuration.LeftJoyconStick.StickButton.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadUp, _configuration.LeftJoycon.DpadUp.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadDown, _configuration.LeftJoycon.DpadDown.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadLeft, _configuration.LeftJoycon.DpadLeft.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadRight, _configuration.LeftJoycon.DpadRight.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Minus, _configuration.LeftJoycon.ButtonMinus.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftShoulder, _configuration.LeftJoycon.ButtonL.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftTrigger, _configuration.LeftJoycon.ButtonZl.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleRightTrigger0, _configuration.LeftJoycon.ButtonSr.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleLeftTrigger0, _configuration.LeftJoycon.ButtonSl.ToInputKey()));

                // Finally configure right joycon
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightStick, _configuration.RightJoyconStick.StickButton.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.A, _configuration.RightJoycon.ButtonA.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.B, _configuration.RightJoycon.ButtonB.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.X, _configuration.RightJoycon.ButtonX.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Y, _configuration.RightJoycon.ButtonY.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Plus, _configuration.RightJoycon.ButtonPlus.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightShoulder, _configuration.RightJoycon.ButtonR.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightTrigger, _configuration.RightJoycon.ButtonZr.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleRightTrigger1, _configuration.RightJoycon.ButtonSr.ToInputKey()));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleLeftTrigger1, _configuration.RightJoycon.ButtonSl.ToInputKey()));
            }
        }

        public void SetLed(uint packedRgb)
        {
            Logger.Info?.Print(LogClass.UI, "SetLed called on an SDL3Keyboard");
        }

        public void SetTriggerThreshold(float triggerThreshold)
        {
            // No operations
        }

        public bool HDRumble(VibrationValue left, VibrationValue right)
        {
            return false;
        }
        
        public bool Rumble(float lowFrequency, float highFrequency, uint durationMs)
        {
            return false;
        }

        public Vector3 GetMotionData(MotionInputId inputId)
        {
            // No operations

            return Vector3.Zero;
        }
    }
}
