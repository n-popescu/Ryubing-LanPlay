using System;

namespace Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.Npad
{
    /// <summary>
    /// Nintendo pad style
    /// </summary>
    [Flags]
    enum NpadStyleTag : uint
    {
        /// <summary>
        /// No type.
        /// </summary>
        None = 0,

        /// <summary>
        /// Pro controller.
        /// </summary>
        FullKey = 1 << 0,

        /// <summary>
        /// Joy-Con controller in handheld mode.
        /// </summary>
        Handheld = 1 << 1,

        /// <summary>
        /// Joy-Con controller in dual mode.
        /// </summary>
        JoyDual = 1 << 2,

        /// <summary>
        /// Joy-Con left controller in single mode.
        /// </summary>
        JoyLeft = 1 << 3,

        /// <summary>
        /// Joy-Con right controller in single mode.
        /// </summary>
        JoyRight = 1 << 4,

        /// <summary>
        /// Poké Ball Plus controller.
        /// </summary>
        Palma = 1 << 6,

        /// <summary>
        /// NES and Famicom controller.
        /// </summary>
        Lark = 1 << 7,

        /// <summary>
        /// NES and Famicom controller in handheld mode.
        /// </summary>
        HandheldLark = 1 << 8,

        /// <summary>
        /// SNES controller.
        /// </summary>
        Lucia = 1 << 9,
        
        // <summary>
        // N64 controller.
        // </summary>
        Lagon = 1 << 10,
        
        // <summary
        // Sega Genesis controller.
        // </summary>
        Lager = 1 << 11,

        /// <summary>
        /// Generic external controller.
        /// </summary>
        SystemExt = 1 << 29,

        /// <summary>
        /// Generic controller.
        /// </summary>
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
