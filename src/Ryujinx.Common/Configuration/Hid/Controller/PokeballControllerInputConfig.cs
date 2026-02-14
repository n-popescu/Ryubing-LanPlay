namespace Ryujinx.Common.Configuration.Hid.Controller
{
    public class PokeballControllerInputConfig<TButton, TStick>
        where TButton : unmanaged
        where TStick : unmanaged
    {
        public TStick Joystick { get; set; }
        public TButton StickButton { get; set; }
        public TButton TopButton { get; set; }
    }
}
