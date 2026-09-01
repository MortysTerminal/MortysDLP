using MortysDLP.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services
{
    /// <summary>Ergebnis einer verifizierten Übertragung. <see cref="Sha256"/> ist immer der
    /// tatsächlich berechnete Wert, unabhängig davon, ob eine erwartete Prüfsumme vorlag.</summary>
    internal sealed record DownloadVerification(string Sha256, long Bytes, bool ChecksumChecked, bool SizeChecked);

    /// <summary>Die heruntergeladene Datei stimmt nicht mit der erwarteten Prüfsumme überein.
    /// Die unvollständig verifizierte Datei ist zu diesem Zeitpunkt bereits gelöscht.</summary>
    internal sealed class ChecksumMismatchException(string expected, string actual)
        : Exception($"Prüfsumme stimmt nicht überein. Erwartet: {expected}, tatsächlich: {actual}")
    {
        public string Expected { get; } = expected;
        public string Actual { get; } = actual;
    }

    /// <summary>
    /// Lädt eine Datei herunter und prüft sie gegen eine erwartete SHA-256-Prüfsumme und/oder
    /// Größe — <b>bevor</b> sie ihren endgültigen Namen trägt. Bewusst allgemein gehalten (URL,
    /// Zielpfad, erwartete Werte kommen vollständig herein), damit Welle 4 dieselbe Klasse für
    /// Werkzeug-Downloads (yt-dlp, ffmpeg, Whisper-Modelle) wiederverwenden kann.
    /// </summary>
    internal static class VerifiedDownload
    {
        private const int BufferSize = 81920;

        /// <summary>Drosselung für <see cref="IProgress{T}"/>-Meldungen — höchstens alle 100 ms
        /// oder alle 0,5 % (02-BEST-PRACTICES.md, Abschnitt 8). Bei einem 3,1-GB-Whisper-Modell
        /// und 80-KB-Puffern wären das sonst zehntausende Meldungen.</summary>
        internal static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(100);
        internal const double ProgressStep = 0.005;

        /// <summary>Reine Entscheidung, losgelöst von Zeit und Netz testbar: Melden, wenn sich
        /// der Anteil seit der letzten Meldung um mindestens <see cref="ProgressStep"/> geändert
        /// hat, oder wenn seitdem mindestens <see cref="ProgressInterval"/> vergangen ist.</summary>
        internal static bool ShouldReportProgress(double fraction, double lastReported, TimeSpan elapsedSinceLastReport) =>
            fraction - lastReported >= ProgressStep || elapsedSinceLastReport >= ProgressInterval;

        /// <summary>
        /// Lädt <paramref name="url"/> nach <c>&lt;targetPath&gt;.part</c>, berechnet die
        /// SHA-256-Prüfsumme streamend beim Schreiben (kein zweites Einlesen) und benennt erst
        /// nach bestandener Prüfung nach <paramref name="targetPath"/> um. Fehlt
        /// <paramref name="expectedSha256"/>, wird nicht blockiert — nur die Größe geprüft
        /// (falls bekannt) und eine Warnung protokolliert. Bei jedem Fehlschlag — Prüfsumme,
        /// Größe, Netzwerkfehler, Abbruch — bleibt weder <c>.part</c> noch eine unvollständige
        /// Zieldatei zurück. <paramref name="client"/> ist nur für Tests gedacht (Standard:
        /// <see cref="Http.Shared"/>), damit sich die Prüfung ohne echten Netzzugriff über
        /// einen gefälschten Handler testen lässt.
        /// </summary>
        /// <param name="progress">Fortschritt als Anteil (0.0–1.0) — siehe
        /// Bleibt unbenutzt, solange
        /// <c>Content-Length</c> fehlt (Gesamtgröße unbekannt).</param>
        public static async Task<DownloadVerification> ToFileAsync(
            string url, string targetPath,
            string? expectedSha256, long? expectedSize,
            IProgress<double>? progress, CancellationToken ct, HttpClient? client = null)
        {
            client ??= Http.Shared;
            UrlSafety.EnsureAllowed(new Uri(url));

            string partPath = targetPath + ".part";

            try
            {
                using var response = await Http.SendWithRetryAsync(
                    client, () => new HttpRequestMessage(HttpMethod.Get, url), ct: ct);
                response.EnsureSuccessStatusCode();

                // Nach automatischen Weiterleitungen ist dies das tatsächlich erreichte Ziel.
                UrlSafety.EnsureAllowed(response.RequestMessage?.RequestUri);

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                long bytesRead = 0;

                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                var progressClock = Stopwatch.StartNew();
                double lastReported = 0.0;

                await using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
                await using (var fileStream = new FileStream(
                    partPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                {
                    var buffer = new byte[BufferSize];
                    int read;
                    while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                        hasher.AppendData(buffer, 0, read);
                        bytesRead += read;

                        if (totalBytes > 0 && progress is not null)
                        {
                            // Math.Clamp: eine unzuverlässige Content-Length darf den Balken
                            // nie über 100 % treiben.
                            double fraction = Math.Clamp((double)bytesRead / totalBytes, 0.0, 1.0);

                            // Gedrosselt: höchstens alle 100 ms oder alle 0,5 % - bei einem
                            // mehrere Gigabyte großen Download mit 80-KB-Puffern sonst
                            // zehntausende Meldungen pro Datei.
                            if (ShouldReportProgress(fraction, lastReported, progressClock.Elapsed))
                            {
                                progress.Report(fraction);
                                lastReported = fraction;
                                progressClock.Restart();
                            }
                        }
                    }

                    // Der letzte Wert wird immer gemeldet, auch wenn die Drosselung ihn
                    // zurückgehalten hat - sonst bleibt eine Fortschrittsanzeige knapp unter
                    // 100 % stehen, obwohl der Download fertig ist.
                    if (totalBytes > 0 && lastReported < 1.0)
                        progress?.Report(1.0);
                }

                string actualSha256 = Convert.ToHexStringLower(hasher.GetHashAndReset());

                bool sizeChecked = expectedSize.HasValue;
                if (sizeChecked && bytesRead != expectedSize!.Value)
                {
                    throw new IOException(
                        $"Heruntergeladene Größe ({bytesRead} Byte) weicht von der erwarteten " +
                        $"Größe ({expectedSize} Byte) ab.");
                }

                bool checksumChecked = !string.IsNullOrEmpty(expectedSha256);
                if (checksumChecked)
                {
                    if (!string.Equals(actualSha256, expectedSha256!.Trim(), StringComparison.OrdinalIgnoreCase))
                        throw new ChecksumMismatchException(expectedSha256, actualSha256);
                }
                else
                {
                    Log.Warn($"Kein erwarteter SHA-256 für '{Path.GetFileName(targetPath)}' vorhanden - " +
                        $"Download wird nur über die Größe geprüft ({bytesRead} Byte).");
                }

                File.Move(partPath, targetPath, overwrite: true);
                return new DownloadVerification(actualSha256, bytesRead, checksumChecked, sizeChecked);
            }
            catch
            {
                DeletePartFile(partPath);
                throw;
            }
        }

        private static void DeletePartFile(string path)
        {
            // Aufräumen ist Best-Effort: Ein liegen gebliebenes .part beim nächsten Versuch
            // überschrieben zu bekommen ist unschön, aber kein neuer Fehler - der eigentliche
            // Fehler (Prüfsumme, Netzwerk, Abbruch) ist bereits unterwegs nach oben.
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* Best-Effort */ }
        }
    }
}
