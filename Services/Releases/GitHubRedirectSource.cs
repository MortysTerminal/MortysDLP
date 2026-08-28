using MortysDLP.Helpers;
using MortysDLP.Models;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Quelle 4: <c>HEAD github.com/{owner}/{repo}/releases/latest</c>, ausgewertet über die
    /// <c>Location</c>-Kopfzeile der Weiterleitung — der billigste denkbare Fallback, ohne
    /// Kontingent und ohne Anfrageinhalt. Braucht zwingend <see cref="Http.NoRedirect"/>: Ein
    /// Client, der Weiterleitungen selbst auflöst, liefert die Endantwort ohne
    /// <c>Location</c>-Kopfzeile, und diese Quelle kann den Tag dann nicht mehr lesen.
    /// </summary>
    internal sealed class GitHubRedirectSource(HttpClient? client = null) : IReleaseSource
    {
        private readonly HttpClient _client = client ?? Http.NoRedirect;

        public string Name => "github-redirect";

        public bool IsAuthoritative => false;

        public async Task<ReleaseInfo?> TryGetLatestAsync(ReleaseQuery query, CancellationToken ct)
        {
            string url = $"https://github.com/{query.Owner}/{query.Repo}/releases/latest";

            try
            {
                using var response = await Http.SendWithRetryAsync(
                    _client, () => new HttpRequestMessage(HttpMethod.Head, url), ct: ct);

                return ParseRedirect(response, query);
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

        internal ReleaseInfo? ParseRedirect(HttpResponseMessage response, ReleaseQuery query)
        {
            int status = (int)response.StatusCode;
            if (status is < 300 or >= 400)
                return null;

            string? location = response.Headers.Location?.ToString();
            if (string.IsNullOrEmpty(location))
                return null;

            string trimmed = location.TrimEnd('/');
            int slash = trimmed.LastIndexOf('/');
            if (slash < 0 || slash == trimmed.Length - 1)
                return null;

            string rawTag = trimmed[(slash + 1)..];
            if (!AppVersion.TryParse(rawTag, out var version))
                return null;

            string? downloadUrl = query.ResolveDownloadUrl(rawTag);
            return new ReleaseInfo(version, downloadUrl, null, null, null, Name, []);
        }
    }
}
