using System;

namespace Ryujinx.HLE.HOS.Services.Hid
{
    [Flags]
    public enum ControllerType
    {
        None,
        ProController = 1 << 0,
        Handheld = 1 << 1,
        JoyconPair = 1 << 2,
        JoyconLeft = 1 << 3,
        JoyconRight = 1 << 4,
        Gamecube = 1 << 5,
        Pokeball = 1 << 6,
        NES = 1 << 7,
        NESHandheld = 1 << 8,
        SNES = 1 << 9,
        N64 = 1 << 10,
        SegaGenesis = 1 << 11,
        // 12 - 28 are reserved
        SystemExternal = 1 << 29,
        System = 1 << 30,
        
        
        // 0 	NpadStyleFullKey (Pro Controller)
        // 1 	NpadStyleHandheld (Joy-Con controller in handheld mode)
        // 2 	NpadStyleJoyDual (Joy-Con controller in dual mode)
        // 3 	NpadStyleJoyLeft (Joy-Con left controller in single mode)
        // 4 	NpadStyleJoyRight (Joy-Con right controller in single mode)
        // 5 	NpadStyleGc (GameCube controller)
        // 6 	NpadStylePalma (Poké Ball Plus controller)
        // 7 	NpadStyleLark (NES/Famicom controller)
        // 8 	NpadStyleHandheldLark (NES/Famicom controller in handheld mode)
        // 9 	NpadStyleLucia (SNES controller)
        // 10 	[12.0.0+] NpadStyleLagon (N64 controller)
        // 11 	[13.0.0+] NpadStyleLager (Sega Genesis controller)
        // 12-28 	Reserved
        // 29 	NpadStyleSystemExt (generic external controller)
        // 30 	NpadStyleSystem (generic controller)
        // 31 	Reserved 
            
        // 0000 0000 0000 [0000 0000 0000 0000 0]00[0]
    }
}
