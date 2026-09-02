using System.Globalization;
using System.Text.RegularExpressions;

namespace MortysDLP.Services
{
    /// <summary>
    /// Führt ffmpeg aus und liest den Fortschritt aus der Standardfehlerausgabe — die
    /// Ausführung war bislang an vier Stellen (<c>DownloadPage</c>, <c>BatchDownloadPage</c>,
    /// <c>ConvertPage</c>, <c>GifPage</c>) fast wortgleich dupliziert, mit derselben
    /// <c>time=HH:MM:SS.ff</c>-Zeile aus der ffmpeg-Ausgabe.
    ///
    /// <para>Bewusst zustandslos (anders als <see cref="YtDlpRunner"/>): Ein Bandbreitenlimit-
    /// bedingter Neustart existiert für lokale ffmpeg-Läufe nicht, ein Abbruch läuft
    /// ausschließlich über den übergebenen <see cref="CancellationToken"/> - <c>ProcessRunner</c>
    /// killt den Prozess dafür bereits selbst.</para>
    ///
    /// <para>Erfolg/Fehlschlag entscheidet weiterhin jeder Aufrufer selbst: Drei der vier
    /// heutigen Kopien werfen bei einem Fehler-Exitcode, <c>ConvertPage</c> setzt stattdessen
    /// nur den Status des betroffenen Elements (mehrere Dateien laufen dort parallel - der
    /// Fehlschlag einer Datei darf die anderen nicht abbrechen). Diese Klasse gibt deshalb das
    /// rohe <see cref="ProcessResult"/> zurück, statt selbst zu werfen.</para>
    /// </summary>
    internal static class FfmpegRunner
    {
        private static readonly Regex TimeRegex =
            new(@"time=(\d{2}:\d{2}:\d{2}\.\d{2})", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Führt ffmpeg aus. <paramref name="onProgress"/> bekommt den Anteil
        /// (0–100) der Gesamtdauer, sooft eine <c>time=</c>-Zeile erkannt wird - nur wenn
        /// <paramref name="totalSeconds"/> &gt; 0 ist, sonst bleibt <paramref name="onProgress"/>
        /// ungenutzt (Gesamtdauer unbekannt, kein sinnvoller Anteil berechenbar).</summary>
        public static async Task<ProcessResult> RunAsync(
            string ffmpegPath,
            IEnumerable<string> arguments,
            double totalSeconds,
            Action<string>? onStdErrLine,
            Action<double>? onProgress,
            CancellationToken ct)
        {
            void OnStdErr(string line)
            {
                onStdErrLine?.Invoke(line);

                if (totalSeconds > 0 && onProgress != null)
                {
                    var m = TimeRegex.Match(line);
                    if (m.Success &&
                        TimeSpan.TryParseExact(m.Groups[1].Value, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture, out var current))
                    {
                        onProgress(Math.Clamp(current.TotalSeconds / totalSeconds * 100.0, 0, 100));
                    }
                }
            }

            var result = await ProcessRunner.RunStreamingAsync(
                ffmpegPath, arguments,
                onStdErr: OnStdErr,
                timeout: null,
                idleTimeout: TimeSpan.FromSeconds(120),
                ct: ct);

            ct.ThrowIfCancellationRequested();
            return result;
        }
    }
}
