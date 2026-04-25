using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;

namespace Ryujinx.Common
{
    // My code is crappy and I know it. Props to VewDev for assisting me in these shenanigans.
    public class StartupTextHelper
    {
        public static void StartupSplash()
        {
            // Ryubing Logo Print
            Logger.Notice.Print(LogClass.Application,  "   ___                 __    _              ");
            Logger.Notice.Print(LogClass.Application, @"  / _ \  __ __ __ __  / /   (_)  ___   ___ _");
            Logger.Notice.Print(LogClass.Application, @" / , _/ / // // // / / _ \ / /  / _ \ / _ `/");
            Logger.Notice.Print(LogClass.Application, @"/_/|_|  \_, / \_,_/ /_.__//_/  /_//_/ \_, / ");
            Logger.Notice.Print(LogClass.Application,  "       /___/                         /___/  ");
            
            // Do some randomizing. Literally just put another entry here to allow it to be picked.
            List<string> splashes = new()
            {
                "Ryubing is my middle name",
                "Giving it 110 percent!",
                "I don't think therefore I don't am!",
                "All hail egg",
                "Insert cringy joke here",
                "ITS RYUBINGING TIME",
                "I hate mondays...",
                "Fantastical!",
                "Now with 100% more humor!",
                //"Wishlist party cosmos!",
                "\"Not S&P approved\" has been approved by S&P",
                "ARE YOU NOT ENTERTAINED?",
                "It's an emulator!",
                "Now the real game begins",
                "Cooked fresh since 2018!",
                "Must've been the wind...",
                "I used to be an adventurer like you before I took an arrow to the knee",
                "Ryubing!",
                "May contain nuts!",
                "May include occasional pop culture references!",
                "100% organically grown!",
                "Have a nice day : )",
                "Spoats car!", // I love my inside jokes.
                "Bottom text",
                "Im sorry dave. I'm afraid I can't do that.",
                "That's no moon",
                "Sir, finishing this fight.",
                "I see how it is...",
                "Space! The final frontier!",
                "If you could not tell already, I love making bad jokes : )",
                "this.",
                "Probably contains no baked beans",
                "Y'all ready for this?",
            };
            Random randomobj = new();
            int randindex = randomobj.Next(0, splashes.Count);
            
            // Print the dang thing
            Logger.Notice.Print(LogClass.Application,  "");
            Logger.Notice.Print(LogClass.Application,  splashes[randindex]);
            Logger.Notice.Print(LogClass.Application,  "");
        }
        
    }
}
