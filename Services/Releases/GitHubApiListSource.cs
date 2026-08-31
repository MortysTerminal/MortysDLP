using MortysDLP.Helpers;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Quelle 2: <c>GET /repos/{owner}/{repo}/releases?per_page=10</c>. Nötig, wenn „latest"
    /// mit <c>404</c> antwortet (z. B. weil ein Repository nur Vorabversionen enthält). Filtert
    /// Entwürfe und (ohne <see cref="ReleaseQuery.AllowPrerelease"/>) Vorabversionen heraus und
    /// wählt unter den verbleibenden Einträgen die höchste <see cref="Models.AppVersion"/> —
    /// nicht den zuerst gelisteten Eintrag, denn GitHub sortiert nach Veröffentlichungsdatum,
    /// nicht nach Versionsnummer. Ist <see cref="ReleaseQuery.ETag"/> gesetzt, wird es als
    /// <c>If-None-Match</c> mitgeschickt.
    /// </summary>
    internal sealed class GitHubApiListSource(HttpClient? client = null) : IReleaseSource
    {
        private readonly HttpClient _client = client ?? Http.Shared;

        public string Name => "github-api-list";

        public bool IsAuthoritative => true;

        public async Task<ReleaseInfo?> TryGetLatestAsync(ReleaseQuery query, CancellationToken ct)
        {
            if (GitHubRateLimit.IsExhausted(DateTimeOffset.UtcNow))
                return null;

            string url = $"https://api.github.com/repos/{query.Owner}/{query.Repo}/releases?per_page=10";

            try
            {
                using var response = await Http.SendWithRetryAsync(
                    _client, () => CreateRequest(url, query.ETag), ct: ct);

                GitHubRateLimit.Observe(response.Headers, DateTimeOffset.UtcNow);

                if (response.StatusCode == HttpStatusCode.NotModified)
                    return new ReleaseInfo(default, null, null, null, null, Name, [], query.ETag, NotModified: true);

                if (!response.IsSuccessStatusCode)
                    return null;

                if (ReleaseResponseGuard.ExceedsLimit(response))
                {
                    Log.Warn($"{Name}: Antwort überschreitet das Größenlimit, verworfen.");
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync(ct);
                return ParseHighestRelease(json, query, response.Headers.ETag?.Tag);
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

        internal ReleaseInfo? ParseHighestRelease(string json, ReleaseQuery query, string? etag = null)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return null;

                ReleaseInfo? best = null;

                foreach (var release in doc.RootElement.EnumerateArray())
                {
                    if (!GitHubApiReleaseJson.IsEligible(release, query))
                        continue;

                    var candidate = GitHubApiReleaseJson.TryParse(release, Name, etag);
                    if (candidate is null)
                        continue;

                    if (best is null || candidate.Version > best.Version)
                        best = candidate;
                }

                return best;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static HttpRequestMessage CreateRequest(string url, string? etag)
        {
            var request = Http.CreateGitHubApiRequest(url);
            if (!string.IsNullOrEmpty(etag))
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            return request;
        }
    }
}
