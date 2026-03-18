using Ryujinx.Common.Configuration.Hid.Keyboard;
using System.Collections.Generic;
using System.Numerics;

using ConfigPhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;

namespace Ryujinx.Input
{
    public static class KeyboardInputMappingHelper
    {
        public readonly record struct KeyboardButtonMapping(GamepadButtonInputId To, Key From)
        {
            public bool IsValid => To is not GamepadButtonInputId.Unbound && From is not Key.Unknown and not Key.Unbound;
        }

        public static KeyboardButtonMapping[] BuildButtonMappings(StandardKeyboardInputConfig configuration) =>
        [
            // Left JoyCon
            new(GamepadButtonInputId.LeftStick,           configuration.LeftJoyconStick.StickButton.ToInputKey()),
            new(GamepadButtonInputId.DpadUp,              configuration.LeftJoycon.DpadUp.ToInputKey()),
            new(GamepadButtonInputId.DpadDown,            configuration.LeftJoycon.DpadDown.ToInputKey()),
            new(GamepadButtonInputId.DpadLeft,            configuration.LeftJoycon.DpadLeft.ToInputKey()),
            new(GamepadButtonInputId.DpadRight,           configuration.LeftJoycon.DpadRight.ToInputKey()),
            new(GamepadButtonInputId.Minus,               configuration.LeftJoycon.ButtonMinus.ToInputKey()),
            new(GamepadButtonInputId.LeftShoulder,        configuration.LeftJoycon.ButtonL.ToInputKey()),
            new(GamepadButtonInputId.LeftTrigger,         configuration.LeftJoycon.ButtonZl.ToInputKey()),
            new(GamepadButtonInputId.SingleRightTrigger0, configuration.LeftJoycon.ButtonSr.ToInputKey()),
            new(GamepadButtonInputId.SingleLeftTrigger0,  configuration.LeftJoycon.ButtonSl.ToInputKey()),

            // Right JoyCon
            new(GamepadButtonInputId.RightStick,          configuration.RightJoyconStick.StickButton.ToInputKey()),
            new(GamepadButtonInputId.A,                   configuration.RightJoycon.ButtonA.ToInputKey()),
            new(GamepadButtonInputId.B,                   configuration.RightJoycon.ButtonB.ToInputKey()),
            new(GamepadButtonInputId.X,                   configuration.RightJoycon.ButtonX.ToInputKey()),
            new(GamepadButtonInputId.Y,                   configuration.RightJoycon.ButtonY.ToInputKey()),
            new(GamepadButtonInputId.Plus,                configuration.RightJoycon.ButtonPlus.ToInputKey()),
            new(GamepadButtonInputId.RightShoulder,       configuration.RightJoycon.ButtonR.ToInputKey()),
            new(GamepadButtonInputId.RightTrigger,        configuration.RightJoycon.ButtonZr.ToInputKey()),
            new(GamepadButtonInputId.SingleRightTrigger1, configuration.RightJoycon.ButtonSr.ToInputKey()),
            new(GamepadButtonInputId.SingleLeftTrigger1,  configuration.RightJoycon.ButtonSl.ToInputKey()),
        ];

        public static (short X, short Y) GetStickValues(ref KeyboardStateSnapshot snapshot, JoyconConfigKeyboardStick<ConfigPhysicalKey> stickConfig)
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

            if (stickX == 0 && stickY == 0)
            {
                return (0, 0);
            }

            Vector2 stick = Vector2.Normalize(new Vector2(stickX, stickY));

            return ((short)(stick.X * short.MaxValue), (short)(stick.Y * short.MaxValue));
        }
    }
}
