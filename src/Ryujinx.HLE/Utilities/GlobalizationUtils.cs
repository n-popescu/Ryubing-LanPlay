using Ryujinx.HLE.HOS.SystemState;

namespace Ryujinx.HLE.Utilities
{
    static class GlobalizationUtils
    {
        public static string SystemLanguageToLanguageKey(SystemLanguage systemLanguage)
        {
            return systemLanguage switch
            {
#pragma warning disable IDE0055 // Disable formatting
                SystemLanguage.Japanese             => "ja",
                SystemLanguage.AmericanEnglish      => "en-US",
                SystemLanguage.French               => "fr",
                SystemLanguage.German               => "de",
                SystemLanguage.Italian              => "it",
                SystemLanguage.Spanish              => "es",
                SystemLanguage.Chinese              => "zh-Hans",
                SystemLanguage.Korean               => "ko",
                SystemLanguage.Dutch                => "nl",
                SystemLanguage.Portuguese           => "pt",
                SystemLanguage.Russian              => "ru",
                SystemLanguage.Taiwanese            => "zh-HansT",
                SystemLanguage.BritishEnglish       => "en-GB",
                SystemLanguage.CanadianFrench       => "fr-CA",
                SystemLanguage.LatinAmericanSpanish => "es-419",
                SystemLanguage.SimplifiedChinese    => "zh-Hans",
                SystemLanguage.TraditionalChinese   => "zh-Hant",
                SystemLanguage.BrazilianPortuguese  => "pt-BR",
                _                                   => "en-US",
#pragma warning restore IDE0055
            };
        }
    }
}
