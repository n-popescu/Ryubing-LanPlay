using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels.Input;
using Ryujinx.Input;
using Ryujinx.Input.Assigner;
using System;
using System.Collections.Generic;
using Button = Ryujinx.Input.Button;
using PhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;

namespace Ryujinx.Ava.UI.Views.Input
{
    public partial class KeyboardInputView : RyujinxControl<KeyboardInputViewModel>
    {
        private ButtonKeyAssigner _currentAssigner;

        public KeyboardInputView()
        {
            InitializeComponent();

            foreach (ILogical visual in SettingButtons.GetLogicalDescendants())
            {
                if (visual is ToggleButton button and not CheckBox)
                {
                    button.IsCheckedChanged += Button_IsCheckedChanged;
                }
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_currentAssigner is { ToggledButton.IsPointerOver: false })
            {
                _currentAssigner.Cancel();
            }
        }

        private void Button_IsCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton button)
                return;

            if (button.IsChecked is true)
            {
                if (_currentAssigner != null && button == _currentAssigner.ToggledButton)
                {
                    return;
                }

                if (_currentAssigner == null)
                {
                    _currentAssigner = new ButtonKeyAssigner(button);

                    Focus(NavigationMethod.Pointer);

                    PointerPressed += MouseClick;

                    IKeyboard keyboard =
                        (IKeyboard)ViewModel.ParentModel.AvaloniaKeyboardDriver.GetGamepad("0"); // Open Avalonia keyboard for cancel operations.
                    IButtonAssigner assigner =
                        new KeyboardKeyAssigner((IKeyboard)ViewModel.ParentModel.SelectedGamepad);

                    _currentAssigner.ButtonAssigned += (_, be) =>
                    {
                        if (be.ButtonValue.HasValue)
                        {
                            Button buttonValue = be.ButtonValue.Value;

                            switch (button.Name)
                            {
                                case "ButtonZl":
                                    ViewModel.Config.ButtonZl = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "ButtonL":
                                    ViewModel.Config.ButtonL = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "ButtonMinus":
                                    ViewModel.Config.ButtonMinus = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "LeftStickButton":
                                    ViewModel.Config.LeftStickButton = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "LeftStickUp":
                                    ViewModel.Config.LeftStickUp = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "LeftStickDown":
                                    ViewModel.Config.LeftStickDown = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "LeftStickRight":
                                    ViewModel.Config.LeftStickRight = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "LeftStickLeft":
                                    ViewModel.Config.LeftStickLeft = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "DpadUp":
                                    ViewModel.Config.DpadUp = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "DpadDown":
                                    ViewModel.Config.DpadDown = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "DpadLeft":
                                    ViewModel.Config.DpadLeft = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "DpadRight":
                                    ViewModel.Config.DpadRight = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "LeftButtonSr":
                                    ViewModel.Config.LeftButtonSr = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "LeftButtonSl":
                                    ViewModel.Config.LeftButtonSl = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "RightButtonSr":
                                    ViewModel.Config.RightButtonSr = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "RightButtonSl":
                                    ViewModel.Config.RightButtonSl = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "ButtonZr":
                                    ViewModel.Config.ButtonZr = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "ButtonR":
                                    ViewModel.Config.ButtonR = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "ButtonPlus":
                                    ViewModel.Config.ButtonPlus = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "ButtonA":
                                    ViewModel.Config.ButtonA = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "ButtonB":
                                    ViewModel.Config.ButtonB = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "ButtonX":
                                    ViewModel.Config.ButtonX = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "ButtonY":
                                    ViewModel.Config.ButtonY = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "RightStickButton":
                                    ViewModel.Config.RightStickButton = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "RightStickUp":
                                    ViewModel.Config.RightStickUp = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "RightStickDown":
                                    ViewModel.Config.RightStickDown = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "RightStickRight":
                                    ViewModel.Config.RightStickRight = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                                case "RightStickLeft":
                                    ViewModel.Config.RightStickLeft = buttonValue.AsHidType<PhysicalKey>();
                                    break;
                            }

                            ViewModel.ParentModel.RefreshModifiedState();
                        }
                    };

                    _currentAssigner.GetInputAndAssign(assigner, keyboard);
                }
                else
                {
                    if (_currentAssigner != null)
                    {
                        _currentAssigner.Cancel();
                        _currentAssigner = null;
                        button.IsChecked = false;
                    }
                }
            }
            else
            {
                _currentAssigner?.Cancel();
                _currentAssigner = null;
            }
        }

        private void MouseClick(object sender, PointerPressedEventArgs e)
        {
            bool shouldUnbind = e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed;

            bool shouldRemoveBinding = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;

            if (shouldRemoveBinding)
            {
                DeleteBind();
            }

            _currentAssigner?.Cancel(shouldUnbind);

            PointerPressed -= MouseClick;
        }

        private void DeleteBind()
        {

            if (_currentAssigner != null)
            {
                Dictionary<string, Action> buttonActions = new()
                {
                    { "ButtonZl", () => ViewModel.Config.ButtonZl = PhysicalKey.Unbound },
                    { "ButtonL", () => ViewModel.Config.ButtonL = PhysicalKey.Unbound },
                    { "ButtonMinus", () => ViewModel.Config.ButtonMinus = PhysicalKey.Unbound },
                    { "LeftStickButton", () => ViewModel.Config.LeftStickButton = PhysicalKey.Unbound },
                    { "LeftStickUp", () => ViewModel.Config.LeftStickUp = PhysicalKey.Unbound },
                    { "LeftStickDown", () => ViewModel.Config.LeftStickDown = PhysicalKey.Unbound },
                    { "LeftStickRight", () => ViewModel.Config.LeftStickRight = PhysicalKey.Unbound },
                    { "LeftStickLeft", () => ViewModel.Config.LeftStickLeft = PhysicalKey.Unbound },
                    { "DpadUp", () => ViewModel.Config.DpadUp = PhysicalKey.Unbound },
                    { "DpadDown", () => ViewModel.Config.DpadDown = PhysicalKey.Unbound },
                    { "DpadLeft", () => ViewModel.Config.DpadLeft = PhysicalKey.Unbound },
                    { "DpadRight", () => ViewModel.Config.DpadRight = PhysicalKey.Unbound },
                    { "LeftButtonSr", () => ViewModel.Config.LeftButtonSr = PhysicalKey.Unbound },
                    { "LeftButtonSl", () => ViewModel.Config.LeftButtonSl = PhysicalKey.Unbound },
                    { "RightButtonSr", () => ViewModel.Config.RightButtonSr = PhysicalKey.Unbound },
                    { "RightButtonSl", () => ViewModel.Config.RightButtonSl = PhysicalKey.Unbound },
                    { "ButtonZr", () => ViewModel.Config.ButtonZr = PhysicalKey.Unbound },
                    { "ButtonR", () => ViewModel.Config.ButtonR = PhysicalKey.Unbound },
                    { "ButtonPlus", () => ViewModel.Config.ButtonPlus = PhysicalKey.Unbound },
                    { "ButtonA", () => ViewModel.Config.ButtonA = PhysicalKey.Unbound },
                    { "ButtonB", () => ViewModel.Config.ButtonB = PhysicalKey.Unbound },
                    { "ButtonX", () => ViewModel.Config.ButtonX = PhysicalKey.Unbound },
                    { "ButtonY", () => ViewModel.Config.ButtonY = PhysicalKey.Unbound },
                    { "RightStickButton", () => ViewModel.Config.RightStickButton = PhysicalKey.Unbound },
                    { "RightStickUp", () => ViewModel.Config.RightStickUp = PhysicalKey.Unbound },
                    { "RightStickDown", () => ViewModel.Config.RightStickDown = PhysicalKey.Unbound },
                    { "RightStickRight", () => ViewModel.Config.RightStickRight = PhysicalKey.Unbound },
                    { "RightStickLeft", () => ViewModel.Config.RightStickLeft = PhysicalKey.Unbound }
                };

                if (buttonActions.TryGetValue(_currentAssigner.ToggledButton.Name, out Action action))
                {
                    action();
                    ViewModel.ParentModel.RefreshModifiedState();
                }
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _currentAssigner?.Cancel();
            _currentAssigner = null;
        }
    }
}
