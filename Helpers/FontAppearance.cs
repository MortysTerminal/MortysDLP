using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace MortysDLP.Helpers
{
    /// <summary>
    /// Steuert das „Schriftbild" der Oberfläche über die Einstellung
    /// <c>Settings.Default.FontAppearance</c>. WPF hat keine Eigenschaft für den
    /// Buchstabenabstand — der einzige echte Hebel ist die Wahl zwischen den beiden
    /// eingebetteten Inter-Schnitten (Inter, normal laufend; Inter Tight, enger gezeichnet),
    /// dazu die Mindest-Zeilenhöhe für mehr vertikale Luft.
    ///
    /// <para>Wirkt zur Laufzeit: <see cref="Apply"/> tauscht die beiden App-weiten Ressourcen
    /// <c>AppFontFamily</c> und <c>AppLineHeight</c> aus. Alle Verweise in XAML hängen an
    /// <c>DynamicResource</c>, deshalb ändert sich die gesamte offene Oberfläche sofort — wie
    /// bei der Sprachumschaltung.</para>
    /// </summary>
    internal static class FontAppearance
    {
        public const string Compact = "compact";
        public const string Standard = "standard";
        public const string Airy = "airy";

        /// <summary>Die drei Werte in Anzeigereihenfolge.</summary>
        public static IReadOnlyList<string> All { get; } = new[] { Compact, Standard, Airy };

        private const string InterTight = "pack://application:,,,/Resources/Fonts/#Inter Tight, Segoe UI Variable, Segoe UI";
        private const string Inter = "pack://application:,,,/Resources/Fonts/#Inter, Segoe UI Variable, Segoe UI";

        // Mindest-Zeilenhöhe für "Luftig". LineStackingStrategy bleibt auf "MaxHeight", der Wert
        // klippt also nie — er hebt nur kleine Textzeilen (11–13 px) auf mehr Höhe an, während
        // Überschriften ihre natürliche Höhe behalten.
        private const double AiryLineHeight = 19.0;

        /// <summary>Der aktuell eingestellte Wert, auf einen der drei gültigen normalisiert.</summary>
        public static string Current
        {
            get
            {
                string value = Properties.Settings.Default.FontAppearance;
                return value is Compact or Standard or Airy ? value : Standard;
            }
        }

        /// <summary>Wendet ein Schriftbild an, ohne die Einstellung zu schreiben (das macht der
        /// Aufrufer). Muss auf dem UI-Thread laufen.</summary>
        public static void Apply(string appearance)
        {
            var resources = Application.Current?.Resources;
            if (resources is null)
                return;

            (string family, double lineHeight) = appearance switch
            {
                Compact => (InterTight, double.NaN),
                Airy => (Inter, AiryLineHeight),
                _ => (Inter, double.NaN),
            };

            resources["AppFontFamily"] = new FontFamily(family);
            resources["AppLineHeight"] = lineHeight;
        }

        /// <summary>Wendet das in den Einstellungen hinterlegte Schriftbild an. Beim Start
        /// aufrufen, bevor das erste Fenster erzeugt wird.</summary>
        public static void ApplyFromSettings() => Apply(Current);
    }
}
