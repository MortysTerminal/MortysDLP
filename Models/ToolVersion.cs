using System;
using System.Collections.Generic;
using System.Globalization;

namespace MortysDLP.Models
{
    /// <summary>
    /// Versionsbegriff für externe Werkzeuge. Trägt bewusst <b>nicht</b> die Annahme, dass eine
    /// Werkzeugversion ordnend ist: yt-dlp meldet <c>2026.08.19</c> (vergleichbar),
    /// ffmpeg meldet <c>7.1-essentials_build-www.gyan.dev</c> (nicht vergleichbar). Beantwortet
    /// genau zwei Fragen — „ist das dieselbe Ausgabe?" (immer beantwortbar, solange beide Seiten
    /// überhaupt einen Wert haben) und „ist das da drüben neuer?" (nur bei ordnender Version,
    /// sonst <c>null</c> = „weiß nicht").
    ///
    /// <para><b>Warum nicht <see cref="AppVersion"/>:</b> <c>AppVersion.TryParse</c> liefert für
    /// <c>"7.1-essentials_build-www.gyan.dev"</c> ein <c>true</c> und liest den Teil hinter dem
    /// Bindestrich als SemVer-Vorab-Suffix — gebaut für den Entwickler-Kanal
    /// (<c>2026.09.01-dev.1</c>). Die installierte ffmpeg-Version gilt damit als Vorabversion und
    /// rangiert <i>kleiner</i> als das schlichte <c>7.1</c>, das der Versionsendpunkt des
    /// Anbieters liefert. Das Ergebnis wäre ein dauerhaftes, nie verschwindendes Update-Angebot.
    /// Der Typ ist nicht kaputt — er ist für diesen Fall das falsche Werkzeug. Deshalb entscheidet
    /// <see cref="ToolVersion"/> selbst, ob eine Version ordnend ist, und delegiert diese Frage
    /// <b>nicht</b> an <c>AppVersion.TryParse</c>.</para>
    ///
    /// <para>Reine Logik: kein Netz, keine Dateien, keine Protokollierung. Wirft unter keiner
    /// Eingabe.</para>
    /// </summary>
    internal readonly struct ToolVersion : IEquatable<ToolVersion>
    {
        /// <summary>Mehr Segmente als das gilt nicht mehr als Zahlenkern — ab da ist die Angabe
        /// eher eine Build-Kennung als eine Version, und ein Vergleich wäre geraten.</summary>
        private const int MaxCoreSegments = 6;

        private readonly string? _raw;
        private readonly int[]? _core;
        private readonly string? _tag;

        private ToolVersion(string raw, int[] core, string? tag)
        {
            _raw = raw;
            _core = core;
            _tag = tag;
        }

        /// <summary>Kein Wert: Werkzeug nicht installiert, oder es hat auf <c>--version</c> nicht
        /// brauchbar geantwortet. Beide Fragen dieses Typs antworten dann mit „nein" bzw.
        /// „weiß nicht" — ein unbekannter Wert darf nie zu einem Update führen.</summary>
        public static ToolVersion Unknown => default;

        public bool HasValue => _raw is not null;

        /// <summary>Die gelesene Angabe nach Trimmen und ohne führendes <c>v</c>/<c>V</c> — für
        /// Anzeige und Protokoll. Nie <c>null</c>, aber leer, wenn <see cref="HasValue"/> falsch ist.</summary>
        public string Raw => _raw ?? string.Empty;

        /// <summary>Der Teil hinter dem Zahlenkern, z. B. <c>"-essentials_build-www.gyan.dev"</c>.
        /// <c>null</c>, wenn die Angabe nur aus Zahlensegmenten besteht.</summary>
        public string? Tag => _tag;

        /// <summary>true, wenn die Angabe mit mindestens einem Zahlensegment beginnt. Ein
        /// Zahlenkern erlaubt die Frage „dieselbe Ausgabe?" auch dann, wenn ein Restanteil
        /// folgt.</summary>
        public bool HasNumericCore => Core.Length > 0;

        /// <summary>true nur, wenn die Angabe <b>ausschließlich</b> aus Zahlensegmenten besteht.
        /// Nur dann ist „neuer als" beantwortbar.</summary>
        public bool IsOrdering => Core.Length > 0 && _tag is null;

        /// <summary>Erstes Segment des Zahlenkerns, <c>null</c> ohne Zahlenkern. Wird gebraucht,
        /// um ein Versionsschema zu erkennen — yt-dlp zählt nach Datum (<c>2026.08.19</c>), und
        /// „erstes Segment ist eine Jahreszahl" ist der billigste Nachweis, dass eine Antwort
        /// überhaupt von yt-dlp stammen kann.</summary>
        public int? FirstSegment => Core.Length > 0 ? Core[0] : null;

        private int[] Core => _core ?? [];

        /// <summary>
        /// Liest <paramref name="text"/> tolerant: ein führendes <c>v</c> vor einer Ziffer fällt
        /// weg, danach wird der führende Zahlenkern (Ziffernfolgen, durch einzelne Punkte
        /// getrennt) gelesen, alles ab der ersten Stelle, die nicht mehr dazugehört, ist
        /// <see cref="Tag"/>. Segmente werden mit <see cref="NumberStyles.None"/> und
        /// <see cref="CultureInfo.InvariantCulture"/> geparst — kein Vorzeichen, kein Leerraum,
        /// kein Tausendertrennzeichen, unabhängig von der eingestellten Sprache.
        /// </summary>
        public static ToolVersion Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Unknown;

            string raw = text.Trim();
            if (raw.Length >= 2 && (raw[0] == 'v' || raw[0] == 'V') && char.IsAsciiDigit(raw[1]))
                raw = raw[1..];

            var segments = new List<int>(MaxCoreSegments);
            int pos = 0;
            int consumed = 0;

            while (segments.Count < MaxCoreSegments)
            {
                if (segments.Count > 0)
                {
                    // Der Punkt gilt erst als verbraucht, wenn dahinter tatsächlich ein
                    // Zahlensegment steht - sonst gehört er zum Restanteil.
                    if (pos >= raw.Length || raw[pos] != '.')
                        break;
                    pos++;
                }

                int start = pos;
                while (pos < raw.Length && char.IsAsciiDigit(raw[pos]))
                    pos++;

                if (pos == start)
                    break;

                if (!int.TryParse(raw[start..pos], NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                    break;

                segments.Add(value);
                consumed = pos;
            }

            string rest = raw[consumed..];
            return new ToolVersion(raw, [.. segments], rest.Length == 0 ? null : rest);
        }

        /// <summary>
        /// Vergleicht ausschließlich die Zahlenkerne, von links nach rechts, fehlende Segmente
        /// zählen als 0. <c>null</c>, wenn mindestens eine Seite keinen Zahlenkern hat — dann ist
        /// nicht einmal Gleichheit über Zahlen entscheidbar.
        /// </summary>
        public int? CompareCore(ToolVersion other)
        {
            if (!HasNumericCore || !other.HasNumericCore)
                return null;

            int[] a = Core;
            int[] b = other.Core;
            int count = Math.Max(a.Length, b.Length);

            for (int i = 0; i < count; i++)
            {
                int va = i < a.Length ? a[i] : 0;
                int vb = i < b.Length ? b[i] : 0;
                if (va != vb)
                    return va.CompareTo(vb);
            }

            return 0;
        }

        /// <summary>
        /// „Ist das dieselbe Ausgabe?" — die Frage, die immer beantwortbar ist. Haben beide Seiten
        /// einen Zahlenkern, entscheidet allein dieser; der Restanteil ist dann eine
        /// <b>Build-Variante</b>, keine Version. Genau das macht den ffmpeg-Fall benutzbar:
        /// Der Anbieter meldet <c>7.1</c>, das installierte Werkzeug meldet
        /// <c>7.1-essentials_build-www.gyan.dev</c> — dieselbe Ausgabe, unterschiedlich
        /// geschrieben. Ein reiner Zeichenkettenvergleich wäre hier dauerhaft „ungleich" und
        /// würde ein Update-Angebot erzeugen, das nie verschwindet.
        ///
        /// <para>Kehrseite, bewusst in Kauf genommen: <c>1.0-beta</c> und <c>1.0</c> gelten als
        /// dieselbe Ausgabe. Für die verwalteten Werkzeuge trifft das nicht zu (yt-dlp und die
        /// GitHub-Werkzeuge melden reine Zahlen, ffmpeg trägt den Anbieter im Restanteil). Ein
        /// Werkzeug, bei dem der Restanteil die Version mitbestimmt, gehört nicht über diese
        /// Frage, sondern über <see cref="IsNewerThan"/> abgefragt.</para>
        ///
        /// <para>Ohne Wert auf einer der beiden Seiten: <c>false</c> — „gleich" wäre eine
        /// Behauptung, die sich nicht belegen lässt.</para>
        /// </summary>
        public bool IsSameRelease(ToolVersion other)
        {
            if (!HasValue || !other.HasValue)
                return false;

            if (CompareCore(other) is int comparison)
                return comparison == 0;

            return string.Equals(Raw, other.Raw, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// „Ist diese Version neuer als <paramref name="other"/>?" — <c>true</c>, <c>false</c>
        /// oder <c>null</c> für „weiß nicht". <c>null</c> entsteht, sobald eine Seite keinen Wert
        /// oder keinen Zahlenkern hat, oder wenn sich die Zahlenkerne unterscheiden, aber
        /// mindestens eine Seite einen Restanteil trägt und damit nicht ordnend ist.
        ///
        /// <para>Gleiche Zahlenkerne ergeben <c>false</c> und nicht <c>null</c>: Dass zwei
        /// Build-Varianten derselben Ausgabe nicht auseinander hervorgehen, ist keine
        /// Unwissenheit, sondern die Antwort.</para>
        ///
        /// <para>Ein <c>null</c> darf beim Aufrufer nie zu einem automatischen Update führen —
        /// höchstens zu einem Angebot.</para>
        /// </summary>
        public bool? IsNewerThan(ToolVersion other)
        {
            if (!HasValue || !other.HasValue)
                return null;

            if (CompareCore(other) is not int comparison)
                return null;

            if (comparison == 0)
                return false;

            if (IsOrdering && other.IsOrdering)
                return comparison > 0;

            return null;
        }

        public override string ToString() => HasValue ? Raw : "unbekannt";

        /// <summary>Zeichengenaue Gleichheit der gelesenen Angabe — <b>nicht</b> die
        /// Ausgaben-Gleichheit. Wer wissen will, ob zwei Angaben dieselbe Ausgabe bezeichnen,
        /// nimmt <see cref="IsSameRelease"/>.</summary>
        public bool Equals(ToolVersion other) =>
            HasValue == other.HasValue &&
            string.Equals(Raw, other.Raw, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is ToolVersion other && Equals(other);

        public override int GetHashCode() =>
            HasValue ? StringComparer.OrdinalIgnoreCase.GetHashCode(Raw) : 0;

        public static bool operator ==(ToolVersion a, ToolVersion b) => a.Equals(b);

        public static bool operator !=(ToolVersion a, ToolVersion b) => !a.Equals(b);
    }
}
