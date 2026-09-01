using MortysDLP.Helpers;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Beschafft eine Prüfsumme aus einem Release-Anhang (z. B. <c>checksums.txt</c> für das
    /// Selbst-Update der Anwendung, <c>SHA2-256SUMS</c> für yt-dlp): Anhang suchen, Ziel gegen
    /// <see cref="UrlSafety"/> prüfen, laden, mit <see cref="ChecksumFile.Find"/> auswerten.
    ///
    /// <para>Vorher zweimal fast wortgleich vorhanden (<c>App.TryFetchChecksumFromAssetsAsync</c>,
    /// <c>YtDlpTool.TryReadChecksumAsync</c>) — mit whisper.cpp und TwitchDownloaderCLI wären es
    /// vier Kopien geworden, dieselbe Bauart, die verstreute, auseinanderlaufende Kopien
    /// erzeugt.</para>
    ///
    /// <para>Wirft nie außer bei Abbruch: Ein Netzwerkfehler beim Lesen der Prüfsummendatei darf
    /// den eigentlichen Download nicht verhindern, nur die Prüfsumme unbekannt lassen.</para>
    /// </summary>
    internal static class ReleaseChecksums
    {
        /// <param name="assets">Anhänge des Releases.</param>
        /// <param name="attachmentName">Name des Anhangs mit den Prüfsummen, z. B.
        /// <c>checksums.txt</c> oder <c>SHA2-256SUMS</c>.</param>
        /// <param name="entryFileName">Datei, für die die Prüfsumme im Anhang gesucht wird.</param>
        /// <param name="logContext">Präfix für die Protokollzeile, z. B. <c>"[yt-dlp]"</c> — leer
        /// lassen, wenn der Aufrufer kein Werkzeug ist.</param>
        public static async Task<string?> TryFetchAsync(
            IReadOnlyList<ReleaseAsset> assets,
            string attachmentName,
            string entryFileName,
            string logContext,
            CancellationToken ct)
        {
            var attachment = assets.FirstOrDefault(a =>
                string.Equals(a.Name, attachmentName, StringComparison.OrdinalIgnoreCase));

            if (attachment is null)
                return null;

            try
            {
                UrlSafety.EnsureAllowed(new Uri(attachment.Url));

                using var response = await Http.SendWithRetryAsync(
                    Http.Shared, () => new HttpRequestMessage(HttpMethod.Get, attachment.Url), ct: ct);

                if (!response.IsSuccessStatusCode)
                    return null;

                string content = await response.Content.ReadAsStringAsync(ct);
                string? sha256 = ChecksumFile.Find(content, entryFileName);

                if (sha256 is null)
                    Log.Warn($"{Prefix(logContext)}{attachmentName} enthält keinen Eintrag für {entryFileName}.");

                return sha256;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warn($"{Prefix(logContext)}{attachmentName} konnte nicht gelesen werden: {ex.Message}", ex);
                return null;
            }
        }

        private static string Prefix(string logContext) =>
            string.IsNullOrEmpty(logContext) ? "" : $"{logContext} ";
    }
}
