using MortysDLP.Helpers;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Tools
{
    /// <summary>Ein Download-Versuch: Adresse mit der erwarteten Größe und Prüfsumme, soweit
    /// bekannt.</summary>
    internal sealed record DownloadCandidate(string Url, long? Size, string? Sha256);

    /// <summary>
    /// Lädt den ersten erreichbaren von mehreren Kandidaten über <see cref="VerifiedDownload"/>.
    /// Ausgewichen wird nur bei einer <see cref="HttpRequestException"/> — Verbindung oder
    /// Namensauflösung gescheitert, oder eine Antwort außerhalb von 2xx (z. B. <c>404</c>), auch
    /// nach den Wiederholungen von <see cref="Http.SendWithRetryAsync"/>. Eine <b>falsche
    /// Prüfsumme</b> oder <b>falsche Größe</b> bricht dagegen sofort ab: Sie ist ein Warnsignal,
    /// das kein zweiter Versuch überdecken soll. Ein Abriss mitten in der Übertragung
    /// (<see cref="System.IO.IOException"/>) und ein Abbruch laufen ebenfalls unverändert durch.
    /// </summary>
    internal static class CandidateDownload
    {
        /// <param name="logId">Kennung für das Protokoll, z. B. die Werkzeug-Id.</param>
        /// <param name="client">Nur für Tests (Standard: <see cref="Http.Shared"/>).</param>
        public static async Task<DownloadVerification> FirstReachableAsync(
            string logId,
            IReadOnlyList<DownloadCandidate> candidates,
            string targetPath,
            IProgress<double>? progress,
            CancellationToken ct,
            HttpClient? client = null)
        {
            if (candidates.Count == 0)
                throw new ArgumentException("Mindestens ein Kandidat erforderlich.", nameof(candidates));

            for (int i = 0; ; i++)
            {
                var candidate = candidates[i];
                Log.Info($"[{logId}] Lade Paket von {candidate.Url} " +
                    $"({(candidate.Sha256 is null ? "ohne bekannte Prüfsumme" : "mit Prüfsumme")}).");

                try
                {
                    return await VerifiedDownload.ToFileAsync(
                        candidate.Url, targetPath, candidate.Sha256, candidate.Size, progress, ct, client);
                }
                catch (HttpRequestException ex) when (i < candidates.Count - 1)
                {
                    Log.Warn($"[{logId}] Paket unter {candidate.Url} nicht erreichbar ({ex.Message}) - " +
                        "nächste Adresse wird versucht.");
                    progress?.Report(0.0);
                }
            }
        }
    }
}
