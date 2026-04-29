using System.Collections.Generic;
using Ryujinx.Common.Logging;
using Gommon;
using Ryujinx.Ava.Systems.Configuration;
using System.Text.Json;

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

            Logger.Notice.Print(LogClass.Application, "");
            Logger.Notice.Print(LogClass.Application, GetSplash());
            Logger.Notice.Print(LogClass.Application, "");
        }

        private static string _Final_Splash = "";

        public static string GetSplash()
        {
            if (string.IsNullOrEmpty(_Final_Splash))
            {
                _Final_Splash = _Get_Lang_Json();
                if (string.IsNullOrEmpty(_Final_Splash))
                {
                    _Final_Splash = "Splash Text";
                }
            }

            return $"{_Final_Splash}";
        }
        
        private static _Splash_Locales _Splash_Json;

        private static string _Get_Lang_Json()
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
                    _Splash_Json = JsonSerializer.Deserialize<_Splash_Locales>(data);
                }
                return _Splash_Json.Locales[ConfigurationState.Instance.UI.LanguageCode.Value].GetRandomElement();
            }
            catch
            {
                return "";
            }
        }

        public struct _Splash_Locales
        {
            public Dictionary<string, List<string>> Locales { get; set; }
        }

    }

}
