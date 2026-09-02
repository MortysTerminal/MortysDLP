using System.Globalization;

namespace MortysDLP.Services
{
    /// <summary>
    /// Fragt Dauer, Video- und Audio-Kennwerte einer Mediendatei per ffprobe ab — eine Stelle
    /// statt der bisher vier leicht unterschiedlichen Kopien in <c>DownloadPage</c>,
    /// <c>BatchDownloadPage</c>, <c>ConvertPage</c> und <c>GifPage</c>.
    ///
    /// <para>Einheitliches Ausgabeformat <c>-of default=noprint_wrappers=1</c>
    /// (Schlüssel=Wert-Zeilen) für alle Abfragen — das nutzte bereits die Mehrheit der
    /// heutigen Kopien. Die einzige Ausnahme war <c>BatchDownloadPage</c>s eigene
    /// Codec/Auflösung-Abfrage, die stattdessen <c>-of csv=p=0</c> verwendete. Gegen eine
    /// echte, mit der installierten ffprobe-Version aufgezeichnete Datei geprüft: Beide
    /// Formate liefern für dieselbe Datei dieselben Werte (siehe <c>MediaProbeTests.cs</c>,
    /// die Vergleichsfälle mit echten Ausgaben beider Formate) — kein tatsächlicher
    /// Unterschied im Ergebnis, nur unnötig zweifacher Code.</para>
    ///
    /// <para>Die eigentliche Textauswertung steckt in separaten, reinen <c>Parse*</c>-Methoden
    /// (kein Prozessstart, keine Ausnahmebehandlung) — testbar mit echten, aufgezeichneten
    /// ffprobe-Ausgaben, ohne dass für den Testlauf selbst ffprobe installiert sein muss.
    /// Dasselbe Prinzip wie bei <see cref="YtDlpProgressParser"/>.</para>
    /// </summary>
    internal static class MediaProbe
    {
        private const string KeyValueFormat = "default=noprint_wrappers=1";

        /// <summary>Ermittelt die Gesamtdauer einer Mediendatei in Sekunden. <c>null</c> bei
        /// jedem Fehler (Datei nicht lesbar, ffprobe nicht gefunden, Dauer nicht ermittelbar) —
        /// der Aufrufer entscheidet selbst über einen Rückfallwert (bisher überall <c>0</c>,
        /// per <c>?? 0</c> am Aufrufort).</summary>
        public static async Task<double?> GetDurationAsync(string ffprobePath, string filePath, CancellationToken ct = default)
        {
            try
            {
                var result = await ProcessRunner.RunAsync(
                    ffprobePath,
                    ["-v", "error", "-show_entries", "format=duration", "-of", $"{KeyValueFormat}:nokey=1", filePath],
                    timeout: TimeSpan.FromSeconds(15), ct: ct);

                return ParseDuration(result.StdOut);
            }
            catch { /* wie bisher an allen vier Aufrufstellen: Fehler bedeutet "unbekannt", kein Absturz */ }
            return null;
        }

        /// <summary>Ermittelt Codec, Breite und Höhe des ersten Video-Streams. Fehlende
        /// Einzelwerte bleiben <c>null</c>/<c>0</c>, ein genereller Fehler liefert
        /// <c>(null, 0, 0)</c> — wie bisher an beiden Aufrufstellen.</summary>
        public static async Task<(string? Codec, int Width, int Height)> GetVideoStreamInfoAsync(
            string ffprobePath, string filePath, CancellationToken ct = default)
        {
            try
            {
                var result = await ProcessRunner.RunAsync(
                    ffprobePath,
                    ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=codec_name,width,height",
                     "-of", KeyValueFormat, filePath],
                    timeout: TimeSpan.FromSeconds(15), ct: ct);

                return ParseVideoStreamInfo(result.StdOut);
            }
            catch
            {
                return (null, 0, 0);
            }
        }

        /// <summary>Ermittelt Samplerate, Kanalzahl und Bitrate (kbit/s) des ersten
        /// Audio-Streams. Kein Audio-Stream (z. B. reines Videomaterial) ist kein Fehler —
        /// ffprobe liefert dann einfach keine Zeilen, alle drei Werte bleiben <c>null</c>.</summary>
        public static async Task<(int? SampleRate, int? Channels, int? BitRateKbps)> GetAudioStreamInfoAsync(
            string ffprobePath, string filePath, CancellationToken ct = default)
        {
            try
            {
                var result = await ProcessRunner.RunAsync(
                    ffprobePath,
                    ["-v", "error", "-select_streams", "a:0", "-show_entries", "stream=sample_rate,channels,bit_rate",
                     "-of", KeyValueFormat, filePath],
                    timeout: TimeSpan.FromSeconds(15), ct: ct);

                return ParseAudioStreamInfo(result.StdOut);
            }
            catch
            {
                return (null, null, null);
            }
        }

        /// <summary>Liest <c>format=duration</c> aus <c>-of default=noprint_wrappers=1:nokey=1</c>
        /// — eine einzelne Zahl, keine Schlüssel=Wert-Zeile (deshalb <c>nokey=1</c>).</summary>
        internal static double? ParseDuration(string ffprobeStdOut) =>
            double.TryParse(ffprobeStdOut.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double duration)
                ? duration
                : null;

        /// <summary>Liest <c>codec_name</c>/<c>width</c>/<c>height</c> aus
        /// <c>-of default=noprint_wrappers=1</c>-Zeilen.</summary>
        internal static (string? Codec, int Width, int Height) ParseVideoStreamInfo(string ffprobeStdOut)
        {
            string? codec = null;
            int width = 0, height = 0;

            foreach (var line in ffprobeStdOut.Split('\n', '\r', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("codec_name=", StringComparison.Ordinal))
                    codec = line["codec_name=".Length..].Trim();
                else if (line.StartsWith("width=", StringComparison.Ordinal) && int.TryParse(line["width=".Length..].Trim(), out int w))
                    width = w;
                else if (line.StartsWith("height=", StringComparison.Ordinal) && int.TryParse(line["height=".Length..].Trim(), out int h))
                    height = h;
            }

            return (string.IsNullOrWhiteSpace(codec) ? null : codec, width, height);
        }

        /// <summary>Liest <c>sample_rate</c>/<c>channels</c>/<c>bit_rate</c> aus
        /// <c>-of default=noprint_wrappers=1</c>-Zeilen. <c>bit_rate</c> steht in ffprobes
        /// Ausgabe in Bit/s, wird hier auf kbit/s umgerechnet.</summary>
        internal static (int? SampleRate, int? Channels, int? BitRateKbps) ParseAudioStreamInfo(string ffprobeStdOut)
        {
            int? sampleRate = null, channels = null, bitRateKbps = null;

            foreach (var line in ffprobeStdOut.Split('\n', '\r', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("sample_rate=", StringComparison.Ordinal) && int.TryParse(line["sample_rate=".Length..].Trim(), out int sr))
                    sampleRate = sr;
                else if (line.StartsWith("channels=", StringComparison.Ordinal) && int.TryParse(line["channels=".Length..].Trim(), out int ch))
                    channels = ch;
                else if (line.StartsWith("bit_rate=", StringComparison.Ordinal) && int.TryParse(line["bit_rate=".Length..].Trim(), out int br))
                    bitRateKbps = br / 1000;
            }

            return (sampleRate, channels, bitRateKbps);
        }
    }
}
