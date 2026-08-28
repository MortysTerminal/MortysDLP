using MortysDLP.Helpers;
using MortysDLP.Models;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Quelle 3: <c>GET github.com/{owner}/{repo}/releases.atom</c>. Ohne GitHub-Kontingent,
    /// weil der Feed nicht über <c>api.github.com</c> läuft — dafür kann er hinter einem CDN
    /// liegen und ein paar Minuten alt sein (deshalb <see cref="IsAuthoritative"/> = false).
    /// Der Tag steht in <c>&lt;entry&gt;&lt;id&gt;</c> hinter dem letzten Schrägstrich
    /// (<c>tag:github.com,2008:Repository/…/2026.06.01</c>); <c>&lt;title&gt;</c> ist nur ein
    /// frei wählbarer Release-Titel und dient als Rückfall.
    /// </summary>
    internal sealed class GitHubAtomFeedSource(HttpClient? client = null) : IReleaseSource
    {
        private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

        private readonly HttpClient _client = client ?? Http.Shared;

        public string Name => "github-atom";

        public bool IsAuthoritative => false;

        public async Task<ReleaseInfo?> TryGetLatestAsync(ReleaseQuery query, CancellationToken ct)
        {
            string url = $"https://github.com/{query.Owner}/{query.Repo}/releases.atom";

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

                string xml = await response.Content.ReadAsStringAsync(ct);
                return ParseFeed(xml, query);
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

        internal ReleaseInfo? ParseFeed(string xml, ReleaseQuery query)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var entry = doc.Root?.Elements(Atom + "entry").FirstOrDefault();
                if (entry is null)
                    return null;

                var resolved = ResolveTagAndVersion(entry);
                if (resolved is null)
                    return null;

                string? changelog = entry.Element(Atom + "content")?.Value;
                string? downloadUrl = query.ResolveDownloadUrl(resolved.Value.RawTag);

                return new ReleaseInfo(resolved.Value.Version, downloadUrl, changelog, null, null, Name, []);
            }
            catch (XmlException)
            {
                return null;
            }
        }

        /// <summary>Bevorzugt den Tag aus <c>&lt;id&gt;</c> — <c>&lt;title&gt;</c> ist ein frei
        /// wählbarer Release-Titel und dient nur als Rückfall, wenn die ID nicht lesbar ist.</summary>
        private static (string RawTag, AppVersion Version)? ResolveTagAndVersion(XElement entry)
        {
            string? idTag = ExtractTagFromId(entry.Element(Atom + "id")?.Value);
            if (idTag != null && AppVersion.TryParse(idTag, out var idVersion))
                return (idTag, idVersion);

            string? title = entry.Element(Atom + "title")?.Value;
            if (title != null && AppVersion.TryParse(title, out var titleVersion))
                return (title, titleVersion);

            return null;
        }

        private static string? ExtractTagFromId(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            int slash = id.LastIndexOf('/');
            return slash >= 0 && slash < id.Length - 1 ? id[(slash + 1)..] : null;
        }
    }
}
