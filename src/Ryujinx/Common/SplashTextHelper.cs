using System.Collections.Generic;
using Ryujinx.Common.Logging;
using Gommon;

namespace Ryujinx.Common
{
    // My code is crappy and I know it. Props to VewDev for assisting me in these shenanigans. - Awesomeangotti
    public class SplashTextHelper
    {
        public static void PrintSplash()
        {
            Logger.Notice.Print(LogClass.Application,  "   ___                 __    _              ");
            Logger.Notice.Print(LogClass.Application, @"  / _ \  __ __ __ __  / /   (_)  ___   ___ _");
            Logger.Notice.Print(LogClass.Application, @" / , _/ / // // // / / _ \ / /  / _ \ / _ `/");
            Logger.Notice.Print(LogClass.Application, @"/_/|_|  \_, / \_,_/ /_.__//_/  /_//_/ \_, / ");
            Logger.Notice.Print(LogClass.Application,  "       /___/                         /___/  ");
            
            Logger.Notice.Print(LogClass.Application,  "");
            Logger.Notice.Print(LogClass.Application,  GetSplash());
            Logger.Notice.Print(LogClass.Application,  "");
        }

        private static string _Final_Splash = "";

        public static string GetSplash()
        {
            if (string.IsNullOrEmpty(_Final_Splash))
            {
                _Final_Splash = _Main_Splashes.GetRandomElement();
            }

            return _Final_Splash;
        }
        
        // This list contains all splashes. Additions are welcome to this list : ) - Awesomeangotti
        private static List<string> _Main_Splashes =  new()
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
                "Removed herobrine",
                "Right to repair!",
                "Programmed in C#!",
                "Forgejo has dethroned Gitlab!",
                "Any ideas what to put here?",
                "Good morning!",
                "Good afternoon!",
                "Good evening!",
                "I hope you are having a great day!",
                "Please insert disc two!",
                "I... AM RYUBING!",
                "Ryubingin' it up",
                "bing bing wahoo",
                "egg",
                "No, lossless scaling is NOT supported",
                "How do you people do anything?",
                "One dollar.",
                "Somebody once told me!",
                "Its that time of the month again!",
                "Brewed from only the finest memes",
                "Async shader compilation would destroy my soul",
                "Trans rights are human rights!",
                ":3",
                "Please connect a controller!",
                "Never gonna give you up!",
                "The game was rigged from the start",
        };
        
        
    }
    
}
