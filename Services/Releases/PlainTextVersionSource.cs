using MortysDLP.Helpers;
using MortysDLP.Models;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Quelle für Endpunkte, deren Antwort ausschließlich aus einer Versionsnummer als Text
    /// besteht (heute: <c>https://www.gyan.dev/ffmpeg/builds/release-version</c>). Die Klasse
    /// kennt keinen konkreten Anbieter — die URL kommt vollständig über
    /// <see cref="ReleaseQuery.PlainTextVersionUrl"/> herein, anders als bei den GitHub-Quellen
    /// ist der Host hier also nicht literal in dieser Klasse festgelegt und wird deshalb vor
    /// jeder Anfrage über <see cref="UrlSafety"/> geprüft. <see cref="IsAuthoritative"/> ist
    /// konfigurierbar, weil dieselbe Bauart für verschiedene Anbieter mit unterschiedlicher
    /// Verlässlichkeit taugt.
    /// </summary>
    internal sealed class PlainTextVersionSource(
        string name, bool isAuthoritative, HttpClient? client = null) : IReleaseSource
    {
        private const int MaxLoggedResponseLength = 80;

        private readonly HttpClient _client = client ?? Http.Shared;

        public string Name { get; } = name;

        public bool IsAuthoritative { get; } = isAuthoritative;

        public async Task<ReleaseInfo?> TryGetLatestAsync(ReleaseQuery query, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query.PlainTextVersionUrl))
                return null;

            if (!Uri.TryCreate(query.PlainTextVersionUrl, UriKind.Absolute, out var uri) ||
                !UrlSafety.IsAllowed(uri))
            {
                Log.Warn($"{Name}: Ziel nicht zulässig: {query.PlainTextVersionUrl}");
                return null;
            }

            try
            {
                using var response = await Http.SendWithRetryAsync(
                    _client, () => new HttpRequestMessage(HttpMethod.Get, uri), ct: ct);

                if (!response.IsSuccessStatusCode)
                    return null;

                if (ReleaseResponseGuard.ExceedsLimit(response))
                {
                    Log.Warn($"{Name}: Antwort überschreitet das Größenlimit, verworfen.");
                    return null;
                }

                string text = await response.Content.ReadAsStringAsync(ct);
                return ParsePlainText(text);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warn($"{Name}: Anfrage fehlgeschlagen.", ex);
                return null;
            }
        }

        /// <summary>Lehnt eine Antwort ab, die keine Versionsnummer ist, statt stillschweigend
        /// <c>null</c> zurückzugeben — der Aufrufer soll im Protokoll sehen können, *warum*
        /// diese Quelle nichts geliefert hat, nicht nur, dass sie es nicht hat.</summary>
        internal ReleaseInfo? ParsePlainText(string text)
        {
            string trimmed = text.Trim();

            if (!AppVersion.TryParse(trimmed, out var version))
            {
                string logged = trimmed.Length > MaxLoggedResponseLength
                    ? trimmed[..MaxLoggedResponseLength] + "…"
                    : trimmed;
                Log.Warn($"{Name}: Antwort ist keine Versionsnummer: '{logged}'.");
                return null;
            }

            return new ReleaseInfo(version, null, null, null, null, Name, []);
        }
    }
}
