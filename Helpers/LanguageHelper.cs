using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MortysDLP.Helpers
{
    public static class LanguageHelper
    {
        public static bool ForceEnglish => Properties.Settings.Default.ForceEnglishLanguage;
        public static void ApplyLanguage(bool forceEnglish)
        {
            CultureInfo culture;
            if (forceEnglish)
            {
                culture = new CultureInfo("en");
            }
            else
            {
                var windowsCulture = CultureInfo.CurrentUICulture;
                culture = windowsCulture.TwoLetterISOLanguageName == "de"
                    ? new CultureInfo("de")
                    : new CultureInfo("en");
            }

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }
}
