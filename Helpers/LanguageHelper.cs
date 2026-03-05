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
            
            System.Diagnostics.Debug.WriteLine($"[LanguageHelper] SelectedLanguage from Settings: '{selectedLanguage}'");
            System.Diagnostics.Debug.WriteLine($"[LanguageHelper] System CurrentUICulture: {CultureInfo.CurrentUICulture.TwoLetterISOLanguageName}");
            
            string language;
            if (!string.IsNullOrEmpty(selectedLanguage) && selectedLanguage != "auto")
            {
                // Verwende gespeicherte Sprache
                language = selectedLanguage;
                System.Diagnostics.Debug.WriteLine($"[LanguageHelper] Using saved language: {language}");
            }
            else
            {
                // Automatische Erkennung: Nutze System-Sprache
                var windowsCulture = CultureInfo.CurrentUICulture;
                language = windowsCulture.TwoLetterISOLanguageName == "de" ? "de" : "en";
                System.Diagnostics.Debug.WriteLine($"[LanguageHelper] Auto-detected language: {language}");
                
                // Legacy-Support: Prüfe ForceEnglishLanguage (falls noch verwendet)
                if (Properties.Settings.Default.ForceEnglishLanguage)
                {
                    language = "en";
                    System.Diagnostics.Debug.WriteLine($"[LanguageHelper] ForceEnglishLanguage is true, using: {language}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[LanguageHelper] Final language to apply: {language}");
            ApplyLanguageCode(language);
        }

        public static void ApplyLanguageCode(string languageCode)
        {
            System.Diagnostics.Debug.WriteLine($"[LanguageHelper] ApplyLanguageCode called with: {languageCode}");
            
            // Setze Culture für .NET
            var culture = new CultureInfo(languageCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            
            System.Diagnostics.Debug.WriteLine($"[LanguageHelper] Culture set to: {CultureInfo.CurrentUICulture.Name}");
            
            // Setze Dictionary-Sprache
            UITexte.UITextDictionary.SetLanguage(languageCode);
            
            System.Diagnostics.Debug.WriteLine($"[LanguageHelper] Dictionary language set to: {UITexte.UITextDictionary.CurrentLanguage}");
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
