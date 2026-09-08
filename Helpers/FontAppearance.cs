using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MortysDLP.Helpers
{
    /// <summary>
    /// Steuert die Schrift der gesamten Oberfläche über die Einstellung
    /// <c>Settings.Default.FontAppearance</c>. WPF hat keine Eigenschaft für den
    /// Buchstabenabstand — „enger/weiter" geht nur über die Wahl des Schnitts (Inter vs
    /// Inter Tight) plus <see cref="System.Windows.Controls.TextBlock.LineHeight"/> für
    /// vertikale Luft. Zusätzlich stehen ein paar Windows-Systemschriften zur Auswahl.
    ///
    /// <para>Wirkt zur Laufzeit: <see cref="Apply"/> zeigt die App-weiten Ressourcen
    /// <c>AppFontFamily</c> und <c>AppLineHeight</c> auf die gewählte Variante. Alle
    /// XAML-Verweise hängen an <c>DynamicResource</c>, deshalb ändert sich die gesamte offene
    /// Oberfläche sofort — wie bei der Sprachumschaltung. Die <see cref="FontFamily"/>-Objekte
    /// selbst kommen aus <c>App.xaml</c> (Schlüssel <c>Font.*</c>), damit der
    /// <c>pack://</c>-URI der eingebetteten Schriften zuverlässig auflöst.</para>
    /// </summary>
    internal static class FontAppearance
    {
        /// <summary>Eine wählbare Schrift-Variante.</summary>
        /// <param name="Key">Wert in der Einstellung. Stabil, nie ändern.</param>
        /// <param name="FontResourceKey">Schlüssel der <see cref="FontFamily"/> in <c>App.xaml</c>.</param>
        /// <param name="LineHeight">Mindest-Zeilenhöhe; <see cref="double.NaN"/> = natürlich.</param>
        internal sealed record Option(string Key, string FontResourceKey, double LineHeight);

        private static readonly Option[] Options =
        [
            new("inter",       "Font.Inter",      double.NaN),
            new("compact",     "Font.InterTight", double.NaN),
            new("airy",        "Font.Inter",      19.0),
            new("segoe",       "Font.Segoe",      double.NaN),
            new("verdana",     "Font.Verdana",    double.NaN),
            new("georgia",     "Font.Georgia",    double.NaN),
            new("tahoma",      "Font.Tahoma",     double.NaN),
        ];

        // Frühere Werte, die es vor 2026-09-08 gab.
        private static readonly Dictionary<string, string> LegacyAliases = new()
        {
            ["standard"] = "inter",
        };

        private const string DefaultKey = "inter";

        /// <summary>Alle Varianten in Anzeigereihenfolge.</summary>
        public static IReadOnlyList<string> All { get; } = Options.Select(o => o.Key).ToArray();

        /// <summary>Der aktuell eingestellte Wert, normalisiert auf eine gültige Variante.</summary>
        public static string Current => Normalize(Properties.Settings.Default.FontAppearance);

        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DefaultKey;
            if (LegacyAliases.TryGetValue(value, out string? mapped))
                return mapped;
            return Options.Any(o => o.Key == value) ? value : DefaultKey;
        }

        /// <summary>Wendet eine Variante an, ohne die Einstellung zu schreiben. UI-Thread.</summary>
        public static void Apply(string appearance)
        {
            var resources = Application.Current?.Resources;
            if (resources is null)
                return;

            Option option = Options.FirstOrDefault(o => o.Key == Normalize(appearance)) ?? Options[0];

            if (resources[option.FontResourceKey] is FontFamily family)
            {
                resources["AppFontFamily"] = family;
                resources["AppLineHeight"] = option.LineHeight;
                Log.Info($"Schrift angewandt: '{option.Key}' ({family.Source}), Zeilenhöhe " +
                    (double.IsNaN(option.LineHeight) ? "natürlich" : option.LineHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            else
            {
                Log.Warn($"Schrift-Ressource '{option.FontResourceKey}' nicht gefunden - Schrift nicht umgestellt.");
            }
        }

        /// <summary>Wendet die gespeicherte Variante an. Beim Start aufrufen, bevor das erste
        /// Fenster erzeugt wird.</summary>
        public static void ApplyFromSettings() => Apply(Current);
    }
}
