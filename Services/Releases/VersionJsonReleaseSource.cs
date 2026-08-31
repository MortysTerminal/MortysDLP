using MortysDLP.Helpers;
using MortysDLP.Models;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Quelle 5: <c>raw.githubusercontent.com/{owner}/{repo}/master/version.json</c> — eine
    /// von Hand gepflegte Datei im Repository, die einzige Quelle mit Prüfsumme. Anders als die
    /// vier Quellen hängt sie an keinem GitHub-API-Kontingent, aber sie kann als
    /// einzige Quelle dauerhaft falsch sein, wenn die Pflege beim Release vergessen wird.
    /// Deshalb <see cref="IsAuthoritative"/> = false und deshalb steht sie am Ende der Kette
    /// — der Rettungsanker, wenn alle GitHub-Endpunkte schweigen, nicht die erste
    /// Anlaufstelle.
    /// </summary>
    internal sealed class VersionJsonReleaseSource(HttpClient? client = null) : IReleaseSource
    {
        private const string Channel = "stable";

        private readonly HttpClient _client = client ?? Http.Shared;

        public string Name => "version-json";

        public bool IsAuthoritative => false;

        public async Task<ReleaseInfo?> TryGetLatestAsync(ReleaseQuery query, CancellationToken ct)
        {
            string url = $"https://raw.githubusercontent.com/{query.Owner}/{query.Repo}/master/version.json";

            try
            {
                using var response = await Http.SendWithRetryAsync(_client, () => CreateRequest(url), ct: ct);

                if (!response.IsSuccessStatusCode)
                    return null;

                if (ReleaseResponseGuard.ExceedsLimit(response))
                {
                    Log.Warn($"{Name}: Antwort überschreitet das Größenlimit, verworfen.");
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync(ct);
                return ParseVersionJson(json);
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

        /// <summary><c>raw.githubusercontent.com</c> liefert sonst bis zu fünf Minuten alte
        /// Inhalte aus einem CDN aus — direkt nach einem Release wäre das die
        /// Vorgängerversion.</summary>
        private static HttpRequestMessage CreateRequest(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            return request;
        }

        /// <summary>Streng geprüft: Jeder Verstoß liefert <c>null</c>, nie eine Ausnahme. Eine
        /// von Hand gepflegte Datei kann jederzeit unvollständig oder falsch sein.</summary>
        internal ReleaseInfo? ParseVersionJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("schemaVersion", out var schemaProp) ||
                    !schemaProp.TryGetInt32(out int schemaVersion) || schemaVersion != 1)
                {
                    Log.Warn($"{Name}: unbekannte oder fehlende schemaVersion.");
                    return null;
                }

                if (!root.TryGetProperty(Channel, out var channel) || channel.ValueKind != JsonValueKind.Object)
                {
                    Log.Warn($"{Name}: Abschnitt '{Channel}' fehlt.");
                    return null;
                }

                if (!channel.TryGetProperty("version", out var versionProp) ||
                    !AppVersion.TryParse(versionProp.GetString(), out var version))
                {
                    Log.Warn($"{Name}: Version nicht lesbar.");
                    return null;
                }

                string? url = channel.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || !UrlSafety.IsAllowed(uri))
                {
                    Log.Warn($"{Name}: URL fehlt oder ist nicht zulässig.");
                    return null;
                }

                string? sha256 = channel.TryGetProperty("sha256", out var shaProp) ? shaProp.GetString() : null;
                if (sha256 != null && !IsValidSha256(sha256))
                {
                    Log.Warn($"{Name}: sha256 hat nicht die erwartete Form.");
                    return null;
                }

                long? size = null;
                if (channel.TryGetProperty("size", out var sizeProp))
                {
                    if (!sizeProp.TryGetInt64(out long sizeValue) || sizeValue <= 0)
                    {
                        Log.Warn($"{Name}: size ist nicht positiv.");
                        return null;
                    }
                    size = sizeValue;
                }

                return new ReleaseInfo(version, url, null, size, sha256, Name, []);
            }
            catch (JsonException)
            {
                Log.Warn($"{Name}: Antwort ist kein gültiges JSON.");
                return null;
            }
        }

        private static bool IsValidSha256(string value) =>
            value.Length == 64 && value.All(Uri.IsHexDigit);
    }
}
