using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using MortysDLP.Properties;

namespace MortysDLP.Helpers
{
    public static class LanguageHelper
    {
        public static void ApplyLanguage()
        {
            string selectedLanguage = Properties.Settings.Default.SelectedLanguage;

            Log.Debug($"SelectedLanguage from Settings: '{selectedLanguage}'");
            Log.Debug($"System CurrentUICulture: {CultureInfo.CurrentUICulture.TwoLetterISOLanguageName}");

            string language;
            if (!string.IsNullOrEmpty(selectedLanguage) && selectedLanguage != "auto")
            {
                // Verwende gespeicherte Sprache
                language = selectedLanguage;
                Log.Debug($"Using saved language: {language}");
            }
            else
            {
                // Automatische Erkennung: Nutze System-Sprache
                var windowsCulture = CultureInfo.CurrentUICulture;
                language = windowsCulture.TwoLetterISOLanguageName == "de" ? "de" : "en";
                Log.Debug($"Auto-detected language: {language}");

                // Legacy-Support: Prüfe ForceEnglishLanguage (falls noch verwendet)
                if (Properties.Settings.Default.ForceEnglishLanguage)
                {
                    language = "en";
                    Log.Debug($"ForceEnglishLanguage is true, using: {language}");
                }
            }

            Log.Debug($"Final language to apply: {language}");
            ApplyLanguageCode(language);
        }

        public static void ApplyLanguageCode(string languageCode)
        {
            Log.Debug($"ApplyLanguageCode called with: {languageCode}");

            // Setze Culture für .NET
            var culture = new CultureInfo(languageCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            Log.Debug($"Culture set to: {CultureInfo.CurrentUICulture.Name}");

            // Setze Dictionary-Sprache
            UITexte.UITextDictionary.SetLanguage(languageCode);

            Log.Debug($"Dictionary language set to: {UITexte.UITextDictionary.CurrentLanguage}");
        }
        
        public static string GetCurrentLanguage()
        {
            return UITexte.UITextDictionary.CurrentLanguage;
        }
        
        public static string GetAutoDetectedLanguage()
        {
            var windowsCulture = CultureInfo.CurrentUICulture;
            return windowsCulture.TwoLetterISOLanguageName == "de" ? "de" : "en";
        }
    }
}
