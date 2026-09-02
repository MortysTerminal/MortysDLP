using System.Globalization;

namespace MortysDLP.Services
{
    /// <summary>Fortschritt eines yt-dlp-Downloads, aus einer strukturierten Zeile gelesen —
    /// nie geraten, nie aus einer für Menschen gedachten Ausgabe zerpflückt.</summary>
    /// <param name="Fraction">Anteil 0.0–1.0. <c>null</c>, wenn yt-dlp die Gesamtgröße nicht
    /// kennt (z. B. bei manchen Livestream-Formaten) — dann lässt sich kein Anteil berechnen,
    /// nur Geschwindigkeit und ggf. Restzeit.</param>
    /// <param name="Eta">Von yt-dlp selbst geschätzte Restzeit, aus einer einzelnen
    /// Momentanmessung berechnet und entsprechend unruhig. <c>null</c>, wenn yt-dlp sie nicht
    /// angibt. Für eine ruhige Anzeige eignet sich <see cref="DownloadSpeedEstimator"/> mit
    /// <see cref="DownloadedBytes"/>/<see cref="RemainingBytes"/> besser.</param>
    /// <param name="SpeedBytesPerSecond">Von yt-dlp selbst gemeldete Momentangeschwindigkeit in
    /// Byte/s - schwankt zwischen zwei Zeilen teils um mehr als das Zehnfache, weil sie pro
    /// Netzwerk-Fragment neu berechnet wird. <c>null</c>, wenn nicht angegeben.</param>
    /// <param name="DownloadedBytes">Bisher heruntergeladene Bytes des aktuellen Streams.
    /// <c>null</c>, wenn yt-dlp keinen Byte-Stand angibt.</param>
    /// <param name="RemainingBytes">Noch ausstehende Bytes bis zur (ggf. geschätzten)
    /// Gesamtgröße. <c>null</c>, wenn sich das nicht berechnen lässt.</param>
    internal readonly record struct YtDlpProgress(
        double? Fraction,
        TimeSpan? Eta,
        double? SpeedBytesPerSecond,
        long? DownloadedBytes,
        long? RemainingBytes);

    /// <summary>
    /// Liest den Fortschritt aus einer fest vorgegebenen <c>--progress-template</c>-Zeile —
    /// maschinenlesbar und versionsstabil, im Gegensatz zum bisherigen Zerlegen der für
    /// Menschen gedachten Konsolenausgabe (<c>[download]  42.3% at 5.21MiB/s ETA 00:12</c>),
    /// die sich zwischen yt-dlp-Versionen bereits mehrfach geändert hat.
    ///
    /// <para>Ausschließlich rohe, numerische Felder — kein <c>_percent_str</c> o. Ä.: Diese
    /// „_"-Felder sind laut yt-dlp intern für die Konsolenanzeige gedacht und tragen
    /// dieselbe Versions-Instabilität, die diese Vorlage gerade vermeiden soll. Der Anteil
    /// wird deshalb selbst aus <c>downloaded_bytes</c>/<c>total_bytes</c> berechnet, mit
    /// <c>total_bytes_estimate</c> als Rückfall, wenn die genaue Gesamtgröße (noch) nicht
    /// bekannt ist.</para>
    ///
    /// <para>Enthält bewusst keine Nutzerdaten (Titel, Dateiname) — nur Zahlen und ein festes
    /// Status-Wort, damit kein Videotitel das Trennzeichen der Vorlage selbst enthalten und
    /// das Parsen durcheinanderbringen kann.</para>
    /// </summary>
    internal static class YtDlpProgressParser
    {
        /// <summary>Wert für <c>--progress-template</c>. Ein eigenes Präfix vor den
        /// Feldern, damit sich eine Vorlagen-Zeile in der übrigen Ausgabe eindeutig erkennen
        /// lässt, ohne mit einer regulären <c>[download]</c>-Zeile oder einer anderen
        /// Werkzeug-Ausgabe zu verwechseln.</summary>
        public const string Template =
            "download:MDLPPROGRESS|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|" +
            "%(progress.total_bytes_estimate)s|%(progress.eta)s|%(progress.speed)s|%(progress.status)s";

        private const string LinePrefix = "MDLPPROGRESS|";
        private const int FieldCount = 6;

        /// <summary>Versucht, <paramref name="line"/> als Vorlagen-Zeile zu lesen. Liefert
        /// <c>false</c> bei jeder Zeile, die nicht dem erwarteten Muster entspricht — auch bei
        /// unvollständigen oder unerwartet aufgebauten Zeilen, nie eine Ausnahme.</summary>
        public static bool TryParse(string line, out YtDlpProgress progress)
        {
            progress = default;

            if (!line.StartsWith(LinePrefix, StringComparison.Ordinal))
                return false;

            string[] fields = line[LinePrefix.Length..].Split('|');
            if (fields.Length != FieldCount)
                return false;

            long? downloaded = ParseLong(fields[0]);
            long? total = ParseLong(fields[1]);
            long? totalEstimate = ParseLong(fields[2]);
            int? etaSeconds = ParseInt(fields[3]);
            double? speed = ParseDouble(fields[4]);
            // fields[5] (Status) wird aktuell nicht ausgewertet - für einen künftigen
            // Bedarf (z. B. "finished" von "downloading" unterscheiden) bereits mitgeführt.

            long? denominator = total ?? totalEstimate;
            double? fraction = downloaded.HasValue && denominator is > 0
                ? Math.Clamp((double)downloaded.Value / denominator.Value, 0.0, 1.0)
                : null;

            TimeSpan? eta = etaSeconds is >= 0 ? TimeSpan.FromSeconds(etaSeconds.Value) : null;

            long? remaining = downloaded.HasValue && denominator is > 0
                ? Math.Max(0, denominator.Value - downloaded.Value)
                : null;

            progress = new YtDlpProgress(fraction, eta, speed, downloaded, remaining);
            return true;
        }

        private static long? ParseLong(string s) =>
            long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : null;

        private static int? ParseInt(string s) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null;

        private static double? ParseDouble(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;
    }
}
