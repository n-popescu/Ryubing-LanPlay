using System;

namespace Ryujinx.HLE.HOS.Services.Hid.Types
{
    [Flags]
    enum AppletFooterUiType : byte
    {
        None,
        HandheldNone,
        HandheldJoyConLeftOnly,
        HandheldJoyConRightOnly,
        HandheldJoyConLeftJoyConRight,
        JoyDual,
        JoyDualLeftOnly,
        JoyDualRightOnly,
        SwitchProController,
        Palma,
        Lark,
        HandheldLark,
        Lucia,
        Lagon,
        Lager,
        Verification,
    }
}
