using System;
using System.Globalization;

namespace MortysDLP.Models
{
    /// <summary>
    /// Toleranter Versionstyp für Release-Tags nach dem Schema <c>JJJJ.MM.TT[.n][-vorab]</c>.
    /// Reine Logik, keine Protokollierung, kein Netz- oder Dateizugriff — wer <see cref="TryParse"/>
    /// aufruft und <c>false</c> erhält, protokolliert selbst, welche Quelle den Wert geliefert hat.
    /// </summary>
    internal readonly struct AppVersion : IComparable<AppVersion>, IEquatable<AppVersion>
    {
        private const int MaxSegments = 6;

        private readonly int[]? _segments;
        private readonly string[]? _prereleaseIdentifiers;
        private readonly string? _raw;

        private AppVersion(string raw, int[] segments, string[] prereleaseIdentifiers)
        {
            _raw = raw;
            _segments = segments;
            _prereleaseIdentifiers = prereleaseIdentifiers;
        }

        /// <summary>Eingabe nach Trimmen und ohne führendes <c>v</c>/<c>V</c> — für die Anzeige.</summary>
        public string Raw => _raw ?? string.Empty;

        public bool IsPrerelease => _prereleaseIdentifiers is { Length: > 0 };

        private int[] Segments => _segments ?? [];

        private string[] PrereleaseIdentifiers => _prereleaseIdentifiers ?? [];

        /// <summary>
        /// Versucht, <paramref name="text"/> als Version zu lesen. Segmente werden ausschließlich
        /// mit <see cref="NumberStyles.None"/> und <see cref="CultureInfo.InvariantCulture"/>
        /// geparst — das schließt Vorzeichen, Leerraum, Tausendertrennzeichen und Exponenten aus
        /// und macht das Ergebnis unabhängig von der eingestellten Sprache. Wirft unter keiner
        /// Eingabe eine Ausnahme.
        /// </summary>
        public static bool TryParse(string? text, out AppVersion version)
        {
            version = default;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string raw = text.Trim();
            if (raw.Length > 0 && (raw[0] == 'v' || raw[0] == 'V'))
                raw = raw[1..];

            if (raw.Length == 0)
                return false;

            string numericPart = raw;
            string[] prereleaseIdentifiers = [];

            int dashIndex = raw.IndexOf('-');
            if (dashIndex >= 0)
            {
                numericPart = raw[..dashIndex];
                string suffix = raw[(dashIndex + 1)..];
                if (suffix.Length == 0)
                    return false;

                prereleaseIdentifiers = suffix.Split('.');
                foreach (string identifier in prereleaseIdentifiers)
                {
                    if (identifier.Length == 0)
                        return false;
                }
            }

            if (numericPart.Length == 0)
                return false;

            string[] segmentTexts = numericPart.Split('.');
            if (segmentTexts.Length > MaxSegments)
                return false;

            var segments = new int[segmentTexts.Length];
            for (int i = 0; i < segmentTexts.Length; i++)
            {
                string segmentText = segmentTexts[i];
                if (segmentText.Length == 0)
                    return false;

                if (!int.TryParse(segmentText, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                    return false;

                segments[i] = value;
            }

            version = new AppVersion(raw, segments, prereleaseIdentifiers);
            return true;
        }

        /// <summary>Wie <see cref="TryParse"/>, wirft aber <see cref="FormatException"/> statt
        /// <c>false</c> zurückzugeben.</summary>
        public static AppVersion Parse(string text)
        {
            if (!TryParse(text, out var version))
                throw new FormatException($"'{text}' ist keine gültige Versionsangabe.");

            return version;
        }

        public override string ToString() => Raw;

        /// <summary>
        /// Vergleicht zuerst die numerischen Segmente von links nach rechts (fehlende Segmente
        /// zählen als 0), dann das Vorab-Suffix: keines schlägt vorhanden, sonst Bezeichner
        /// paarweise (numerisch vor alphanumerisch, sonst ordinal ohne Groß-/Kleinschreibung),
        /// zuletzt die Anzahl der Bezeichner.
        /// </summary>
        public int CompareTo(AppVersion other)
        {
            int[] a = Segments;
            int[] b = other.Segments;

            int segmentCount = Math.Max(a.Length, b.Length);
            for (int i = 0; i < segmentCount; i++)
            {
                int va = i < a.Length ? a[i] : 0;
                int vb = i < b.Length ? b[i] : 0;
                int cmp = va.CompareTo(vb);
                if (cmp != 0)
                    return cmp;
            }

            bool thisHasSuffix = IsPrerelease;
            bool otherHasSuffix = other.IsPrerelease;
            if (thisHasSuffix != otherHasSuffix)
                return thisHasSuffix ? -1 : 1; // ohne Suffix ist grösser

            if (!thisHasSuffix)
                return 0;

            string[] ia = PrereleaseIdentifiers;
            string[] ib = other.PrereleaseIdentifiers;

            int identifierCount = Math.Min(ia.Length, ib.Length);
            for (int i = 0; i < identifierCount; i++)
            {
                int cmp = ComparePrereleaseIdentifier(ia[i], ib[i]);
                if (cmp != 0)
                    return cmp;
            }

            return ia.Length.CompareTo(ib.Length);
        }

        private static int ComparePrereleaseIdentifier(string a, string b)
        {
            bool aNumeric = int.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out int aValue);
            bool bNumeric = int.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out int bValue);

            if (aNumeric && bNumeric)
                return aValue.CompareTo(bValue);

            if (aNumeric != bNumeric)
                return aNumeric ? -1 : 1; // numerischer Bezeichner rangiert unter alphanumerischem

            return StringComparer.OrdinalIgnoreCase.Compare(a, b);
        }

        public bool Equals(AppVersion other) => CompareTo(other) == 0;

        public override bool Equals(object? obj) => obj is AppVersion other && Equals(other);

        // Muss zu Equals/CompareTo konsistent sein: Segmente werden ohne nachfolgende
        // Nullen gehasht (2026.6 und 2026.6.0 sollen denselben Hash liefern), Vorab-Bezeichner
        // numerisch bzw. ordinal ohne Groß-/Kleinschreibung - passend zur Vergleichslogik oben.
        public override int GetHashCode()
        {
            int[] segments = Segments;
            int lastNonZero = -1;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] != 0)
                    lastNonZero = i;
            }

            var hash = new HashCode();
            for (int i = 0; i <= lastNonZero; i++)
                hash.Add(segments[i]);

            foreach (string identifier in PrereleaseIdentifiers)
            {
                if (int.TryParse(identifier, NumberStyles.None, CultureInfo.InvariantCulture, out int numeric))
                    hash.Add(numeric);
                else
                    hash.Add(identifier.ToUpperInvariant());
            }

            return hash.ToHashCode();
        }

        public static bool operator <(AppVersion a, AppVersion b) => a.CompareTo(b) < 0;
        public static bool operator >(AppVersion a, AppVersion b) => a.CompareTo(b) > 0;
        public static bool operator <=(AppVersion a, AppVersion b) => a.CompareTo(b) <= 0;
        public static bool operator >=(AppVersion a, AppVersion b) => a.CompareTo(b) >= 0;
        public static bool operator ==(AppVersion a, AppVersion b) => a.Equals(b);
        public static bool operator !=(AppVersion a, AppVersion b) => !a.Equals(b);
    }
}
