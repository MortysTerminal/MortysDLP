using Microsoft.Win32;
using MortysDLP.Helpers;
using System;
using System.Windows;
using System.Windows.Media;

namespace MortysDLP.Services
{
    /// <summary>
    /// Hält die vier Statusfarben-Ressourcen (<c>SuccessBrush</c>, <c>WarningBrush</c>,
    /// <c>ErrorBrush</c>, <c>RunningBrush</c>) auf dem hellen bzw. dunklen Wert. Anders als die
    /// eingebauten Fluent-Ressourcen (z. B. <c>CardBackgroundFillColorDefaultBrush</c>) kennt
    /// WPF für eigene Ressourcen kein <c>ThemeDictionaries</c>-Tag (das ist WinUI/UWP, nicht
    /// klassisches WPF - geprüft gegen .NET 10) - deshalb wird hier per Code nachgeführt, mit
    /// einem Ersetzen des jeweiligen Ressourcen-Eintrags (Details in <see cref="Apply"/>): Jede
    /// Stelle, die den Pinsel per <c>FindResource</c> holt, sobald sich ein Status ändert, sieht
    /// die neue Farbe dadurch automatisch.
    /// </summary>
    internal static class StatusColorSync
    {
        private static bool _started;

        private static readonly (string Key, string Light, string Dark)[] Colors =
        [
            ("SuccessBrush", "#0F7B0F", "#6CCB5F"),
            ("WarningBrush", "#9D5D00", "#FCE100"),
            ("ErrorBrush",   "#C42B1C", "#FF99A4"),
            ("RunningBrush", "#e67e50", "#ffe44e"), // BrandOrange (hell) / BrandYellow (dunkel)
        ];

        /// <summary>Setzt die Statusfarben einmal auf den aktuellen Windows-Themazustand und
        /// hält sie danach über <see cref="SystemEvents.UserPreferenceChanged"/> aktuell. Mehrfache
        /// Aufrufe sind ungefährlich - nur der erste registriert den Ereignisbehandler.</summary>
        public static void Start()
        {
            Apply();

            if (_started)
                return;
            _started = true;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        /// <summary>Entfernt den Ereignisbehandler. <see cref="SystemEvents"/> ist statisch und
        /// hält sonst über die Lebensdauer des Prozesses eine Referenz - beim regulären Beenden
        /// unkritisch, aber sauberer Stil.</summary>
        public static void Stop()
        {
            if (!_started)
                return;
            _started = false;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }

        private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General)
                return;

            // Kommt nicht vom UI-Thread - SystemEvents nutzt einen eigenen Nachrichtenfenster-Thread.
            Application.Current?.Dispatcher.BeginInvoke(Apply);
        }

        private static void Apply()
        {
            bool light = IsLightTheme();
            var resources = Application.Current?.Resources;
            if (resources is null)
                return;

            foreach (var (key, lightHex, darkHex) in Colors)
            {
                // Ein in XAML angelegter SolidColorBrush ohne DynamicResource-Inhalt wird von
                // WPF automatisch eingefroren (Freezable-Optimierung) - .Color lässt sich dann
                // nicht mehr setzen ("schreibgeschützter Zustand"). Deshalb wird der Eintrag
                // ersetzt statt mutiert; jede Stelle im Code holt den Pinsel ohnehin erst im
                // Moment eines Statuswechsels per FindResource neu, nicht per DynamicResource-
                // Bindung, sieht die neue Farbe also beim nächsten Statuswechsel.
                var color = (Color)ColorConverter.ConvertFromString(light ? lightHex : darkHex)!;
                resources[key] = new SolidColorBrush(color);
            }
        }

        /// <summary>Liest die Windows-Einstellung „Standard-App-Modus" aus der Registrierung.
        /// Fehlt der Wert oder lässt er sich nicht lesen (ältere Windows-Version, eingeschränkte
        /// Rechte), gilt hell als Ausweich - der Windows-Standard bei einer Neuinstallation.</summary>
        private static bool IsLightTheme()
        {
            const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath);
                if (key?.GetValue("AppsUseLightTheme") is int value)
                    return value != 0;
                return true;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
            {
                Log.Warn("Windows-Themamodus konnte nicht gelesen werden, verwende hell.", ex);
                return true;
            }
        }
    }
}
