using System;

namespace Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.Npad
{
    [Flags]
    enum DeviceType
    {
        None = 0,
        SwitchProController = 1 << 0,
        HandheldLeft = 1 << 2,
        HandheldRight = 1 << 3,
        JoyConLeft = 1 << 4,
        JoyConRight = 1 << 5,
        Palma = 1 << 6,
        LarkHvcLeft =  1 << 7,
        LarkHvcRight = 1 << 8,
        LarkNesLeft = 1 << 9,
        LarkNesRight = 1 << 10,
        HandheldLarkHvcLeft = 1 << 11,
        HandheldLarkHvcRight = 1 << 12,
        HandheldLarkNesLeft = 1 << 13,
        HandheldLarkNesRight = 1 << 14,
        Lucia = 1 << 15,
        Lagon = 1 << 16,
        Lager = 1 << 17,
        System = 1 << 31,
    }
}
