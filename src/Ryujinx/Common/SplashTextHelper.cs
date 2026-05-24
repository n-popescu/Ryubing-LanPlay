using System.Collections.Generic;
using Ryujinx.Common.Logging;
using Gommon;
using Ryujinx.Ava.Systems.Configuration;
using System;
using System.Text.Json;

namespace Ryujinx.Common
{
    // My code is crappy and I know it. Props to VewDev for assisting me in these shenanigans. - Awesomeangotti
    public class SplashTextHelper
    {
        // These variables will be set to something once I figure out how to implement user selectable options. Perhaps just check and assign at boot?
        private static bool s_loadingSplashEnabled = true;
        
        private static bool s_logSplashEnabled = true;
        
        private static bool s_titleSplashEnabled = true;
        
        private static string s_finalSplash = "";

        private static string GetSplash()
        {
            if (string.IsNullOrEmpty(s_finalSplash))
            {
                s_finalSplash = _GetLangJson();
                if (string.IsNullOrEmpty(s_finalSplash))
                {
                    s_finalSplash = "Splash Text";
                }
            }
            return $"{s_finalSplash}";
        }

        public static string GetTitleSplash()
        {
            if (s_titleSplashEnabled)
            {
                if (OperatingSystem.IsMacOS())
                {
                    return "";
                }
                
                return $" - {GetSplash()}";
            }
            return "";

        }
        
        public static string GetLoadingSplash()
        {
            if (s_loadingSplashEnabled)
            {
                return $"\"{GetSplash()}\"";    
            }
            return "";
        }
        
        public static void PrintSplash()
        {
            Logger.Notice.Print(LogClass.Application,  "   ___                 __    _              ");
            Logger.Notice.Print(LogClass.Application, @"  / _ \  __ __ __ __  / /   (_)  ___   ___ _");
            Logger.Notice.Print(LogClass.Application, @" / , _/ / // // // / / _ \ / /  / _ \ / _ `/");
            Logger.Notice.Print(LogClass.Application, @"/_/|_|  \_, / \_,_/ /_.__//_/  /_//_/ \_, / ");
            Logger.Notice.Print(LogClass.Application,  "       /___/                         /___/  ");
            
            if (s_logSplashEnabled)
            {
                Logger.Notice.Print(LogClass.Application, "");
                Logger.Notice.Print(LogClass.Application, GetSplash());
                Logger.Notice.Print(LogClass.Application, "");
            }
        }
        
        private static SplashLocales s_SplashJson;

        private static string _GetLangJson()
        {
            try
            {
                foreach (string uri in EmbeddedResources.GetAllAvailableResources("Ryujinx/Assets/Splashes", ".json"))
                {
                    string data;
                    string path = uri[..^".json".Length];
                    path = path.Replace('.', '/');
                    path = path.Append(".json");
                    data = EmbeddedResources.ReadAllText(path);
                    s_SplashJson = JsonSerializer.Deserialize<SplashLocales>(data);
                }
                return s_SplashJson.Locales[ConfigurationState.Instance.UI.LanguageCode.Value].GetRandomElement();
            }
            catch
            {
                return "";
            }
        }

        private struct SplashLocales
        {
            public Dictionary<string, List<string>> Locales { get; }
        }

    }

}
