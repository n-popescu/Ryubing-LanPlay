namespace Ryujinx.Input.Assigner
{
    /// <summary>
    /// <see cref="IButtonAssigner"/> implementation for <see cref="IKeyboard"/>.
    /// </summary>
    public class KeyboardKeyAssigner : IButtonAssigner
    {
        private readonly IKeyboard _keyboard;

        private KeyboardStateSnapshot _keyboardState;

        public KeyboardKeyAssigner(IKeyboard keyboard)
        {
            _keyboard = keyboard;
        }

        public void Initialize() { }

        public void ReadInput()
        {
            _keyboardState = _keyboard.GetKeyboardStateSnapshot();
        }

        public bool IsAnyButtonPressed()
        {
            return GetPressedButton() is not null;
        }

        public bool ShouldCancel()
        {
            return _keyboardState.IsPressed(Key.Escape);
        }

        public Button? GetPressedButton()
        {
            // On some layouts (for example AltGr on Windows), Right Alt is reported as Ctrl+Alt.
            // Prefer AltRight in that case so the binding reflects the physical key used.
            if (_keyboardState.IsPressed(Key.ControlLeft) && _keyboardState.IsPressed(Key.AltRight))
            {
                return !ShouldCancel() ? new Button(Key.AltRight) : null;
            }

            Button? keyPressed = null;

            for (Key key = Key.Unknown; key < Key.Count; key++)
            {
                if (_keyboardState.IsPressed(key))
                {
                    keyPressed = new(key);
                    break;
                }
            }

            return !ShouldCancel() ? keyPressed : null;
        }
    }
}
