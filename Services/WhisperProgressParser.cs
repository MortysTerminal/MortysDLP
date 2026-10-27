using System.Globalization;
using System.Text.RegularExpressions;

namespace MortysDLP.Services
{
    /// <summary>
    /// Liest Fortschrittsangaben aus der Ausgabe von whisper-cli und ffmpeg — rein und ohne
    /// Seiteneffekte, damit sich die Zeilenformate ohne laufenden Prozess testen lassen.
    /// Alle Methoden nutzen die invariante Kultur und werfen bei unbrauchbaren Zeilen nie,
    /// sondern liefern <c>false</c>.
    /// </summary>
    internal static class WhisperProgressParser
    {
        /// <summary>Anteil der Audio-Extraktion am Gesamtfortschritt, wenn extrahiert wird.</summary>
        public const double ExtractionWeight = 0.10;

        // whisper-cli: fprintf(stderr, "%s: progress = %3d%%\n", ...) — vor der Zahl können
        // also Leerzeichen stehen ("progress =   5%").
        private static readonly Regex PercentRegex =
            new(@"progress\s*=\s*(\d{1,3})\s*%", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Segmentzeile auf stdout: "[00:00:00.000 --> 00:00:04.640]   text"
        private static readonly Regex SegmentRegex =
            new(@"^\s*\[\s*\d{1,6}:\d{2}:\d{2}\.\d{1,3}\s*-->\s*(\d{1,6}):(\d{2}):(\d{2})\.(\d{1,3})\s*\]",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // ffmpeg-Statuszeile (stderr): "time=00:01:23.45"; "time=N/A" passt bewusst nicht.
        private static readonly Regex FfmpegTimeRegex =
            new(@"time=(\d{1,6}):(\d{2}):(\d{2})(?:\.(\d+))?", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Liest <c>progress = NN%</c> (stdout oder stderr). Liefert den Anteil 0.0–1.0;
        /// Werte über 100 gelten als unbrauchbar.</summary>
        public static bool TryParsePercent(string? line, out double fraction)
        {
            fraction = 0;
            if (string.IsNullOrEmpty(line)) return false;

            var m = PercentRegex.Match(line);
            if (!m.Success) return false;
            if (!int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int pct))
                return false;
            if (pct > 100) return false;

            fraction = pct / 100.0;
            return true;
        }

        /// <summary>Liest das Ende einer Segmentzeile <c>[hh:mm:ss.mmm --> hh:mm:ss.mmm]</c>.</summary>
        public static bool TryParseSegmentEnd(string? line, out TimeSpan end)
        {
            end = TimeSpan.Zero;
            if (string.IsNullOrEmpty(line)) return false;

            var m = SegmentRegex.Match(line);
            if (!m.Success) return false;
            return TryBuild(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, out end);
        }

        /// <summary>Liest <c>time=HH:MM:SS.xx</c> aus einer ffmpeg-Statuszeile.</summary>
        public static bool TryParseFfmpegTime(string? line, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            if (string.IsNullOrEmpty(line)) return false;

            var m = FfmpegTimeRegex.Match(line);
            if (!m.Success) return false;
            return TryBuild(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, out time);
        }

        /// <summary>Teilt eine Zeit durch die Gesamtdauer; auf 0.0–1.0 geklammert.
        /// <c>false</c>, wenn die Dauer unbekannt oder nicht positiv ist.</summary>
        public static bool TryGetFraction(TimeSpan position, double? totalSeconds, out double fraction)
        {
            fraction = 0;
            if (totalSeconds is not > 0) return false;
            fraction = Math.Clamp(position.TotalSeconds / totalSeconds.Value, 0.0, 1.0);
            return true;
        }

        /// <summary>
        /// Rechnet den Anteil einer Phase in den Gesamtanteil um: mit Audio-Extraktion belegt
        /// sie 0–10 %, die Transkription 10–100 %; ohne Extraktion (Audiodatei) läuft die
        /// Transkription über 0–100 %.
        /// </summary>
        public static double ToOverall(double phaseFraction, bool isExtractionPhase, bool hasExtraction)
        {
            double f = Math.Clamp(phaseFraction, 0.0, 1.0);
            if (!hasExtraction) return f;
            return isExtractionPhase
                ? f * ExtractionWeight
                : ExtractionWeight + f * (1.0 - ExtractionWeight);
        }

        private static bool TryBuild(string h, string m, string s, string fraction, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (!int.TryParse(h, NumberStyles.None, CultureInfo.InvariantCulture, out int hh) ||
                !int.TryParse(m, NumberStyles.None, CultureInfo.InvariantCulture, out int mm) ||
                !int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int ss))
                return false;
            if (mm > 59 || ss > 59) return false;

            double frac = 0;
            if (fraction.Length > 0 &&
                !double.TryParse("0." + fraction, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out frac))
                return false;

            result = new TimeSpan(hh, mm, ss) + TimeSpan.FromSeconds(frac);
            return true;
        }
    }
}
