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
    /// Quelle 1: <c>GET /repos/{owner}/{repo}/releases/latest</c>. Die reichhaltigste Quelle
    /// (Version, Assets, Changelog) — aber mit GitHub-Kontingent (60/h/IP) und <c>404</c>, wenn
    /// ein Repository nur Vorabversionen enthält. Ist <see cref="ReleaseQuery.ETag"/> gesetzt,
    /// wird es als <c>If-None-Match</c> mitgeschickt — ein <c>304</c> kostet kein
    /// Kontingent und wird als <see cref="ReleaseInfo.NotModified"/> gemeldet.
    /// </summary>
    internal sealed class GitHubApiLatestSource(HttpClient? client = null) : IReleaseSource
    {
        private readonly HttpClient _client = client ?? Http.Shared;

        public string Name => "github-api-latest";

        public bool IsAuthoritative => true;

        public async Task<ReleaseInfo?> TryGetLatestAsync(ReleaseQuery query, CancellationToken ct)
        {
            if (GitHubRateLimit.IsExhausted(DateTimeOffset.UtcNow))
                return null;

            string url = $"https://api.github.com/repos/{query.Owner}/{query.Repo}/releases/latest";

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
                return ParseRelease(json, response.Headers.ETag?.Tag);
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

        internal ReleaseInfo? ParseRelease(string json, string? etag = null)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return GitHubApiReleaseJson.TryParse(doc.RootElement, Name, etag);
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
