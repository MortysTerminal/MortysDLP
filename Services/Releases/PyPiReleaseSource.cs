using MortysDLP.Helpers;
using MortysDLP.Models;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Quelle für PyPI-Pakete: <c>GET https://pypi.org/pypi/{paket}/json</c>, liest
    /// <c>info.version</c>. Die wertvollste Ausweichquelle überhaupt, weil sie völlig
    /// unabhängig von GitHub läuft (kein Kontingent, keine gemeinsame Infrastruktur) — aber
    /// auch die ungenaueste: Sie liefert nur eine Versionsnummer, keine Asset-Liste und
    /// keinen Changelog, und ein PyPI-Release muss nicht im selben Moment wie der zugehörige
    /// GitHub-Release erscheinen. Deshalb <see cref="IsAuthoritative"/> = false.
    /// </summary>
    internal sealed class PyPiReleaseSource(HttpClient? client = null) : IReleaseSource
    {
        private readonly HttpClient _client = client ?? Http.Shared;

        public string Name => "pypi";

        public bool IsAuthoritative => false;

        public async Task<ReleaseInfo?> TryGetLatestAsync(ReleaseQuery query, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query.PackageName))
                return null;

            string url = $"https://pypi.org/pypi/{Uri.EscapeDataString(query.PackageName)}/json";

            // Der Paketname kommt aus der Anfrage, nicht aus einer festen Zeichenkette in
            // dieser Klasse - anders als bei den GitHub-Quellen ist das Ziel damit nicht rein
            // literal vorgegeben und wird deshalb vor dem Senden geprüft.
            if (!UrlSafety.IsAllowed(new Uri(url)))
            {
                Log.Warn($"{Name}: Ziel nicht zulässig: {url}");
                return null;
            }

            try
            {
                using var response = await Http.SendWithRetryAsync(
                    _client, () => new HttpRequestMessage(HttpMethod.Get, url), ct: ct);

                if (!response.IsSuccessStatusCode)
                    return null;

                if (ReleaseResponseGuard.ExceedsLimit(response))
                {
                    Log.Warn($"{Name}: Antwort überschreitet das Größenlimit, verworfen.");
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync(ct);
                return ParsePyPiJson(json);
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

        /// <summary>Liest ausschließlich <c>info.version</c> — die PyPI-Antwort enthält auch
        /// eine <c>releases</c>-Liste mit Downloads, aber die Auswahl eines konkreten Assets
        /// ist nicht Aufgabe dieser Quelle (sie meldet <c>DownloadUrl = null</c>).</summary>
        internal ReleaseInfo? ParsePyPiJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("info", out var info) ||
                    info.ValueKind != JsonValueKind.Object)
                    return null;

                if (!info.TryGetProperty("version", out var versionProp) ||
                    !AppVersion.TryParse(versionProp.GetString(), out var version))
                    return null;

                return new ReleaseInfo(version, null, null, null, null, Name, []);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
