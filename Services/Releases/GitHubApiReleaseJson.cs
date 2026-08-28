using MortysDLP.Models;
using System.Collections.Generic;
using System.Text.Json;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Gemeinsames Parsen einzelner GitHub-Release-JSON-Objekte für
    /// <see cref="GitHubApiLatestSource"/> und <see cref="GitHubApiListSource"/> — beide lesen
    /// dieselbe Objektform, nur aus unterschiedlichen Endpunkten (ein Objekt bzw. ein Array
    /// aus Objekten). Wirft nie: ein unbrauchbares Objekt liefert <c>null</c>.
    /// </summary>
    internal static class GitHubApiReleaseJson
    {
        /// <summary>true, wenn ein Eintrag aus der Listen-Antwort nach den Filterregeln
        /// überhaupt in Frage kommt: kein Entwurf, und eine Vorabversion nur, wenn
        /// <see cref="ReleaseQuery.AllowPrerelease"/> gesetzt ist.</summary>
        public static bool IsEligible(JsonElement release, ReleaseQuery query)
        {
            if (release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
                return false;

            if (!query.AllowPrerelease &&
                release.TryGetProperty("prerelease", out var prerelease) &&
                prerelease.ValueKind == JsonValueKind.True)
                return false;

            return true;
        }

        /// <summary>Parst ein einzelnes Release-JSON-Objekt. Liefert <c>null</c>, wenn der Tag
        /// fehlt oder von <see cref="AppVersion.TryParse"/> nicht gelesen werden kann. Setzt
        /// <c>DownloadUrl</c>, <c>ExpectedSize</c> und <c>Sha256</c> bewusst nicht — welches
        /// Asset gemeint ist, entscheidet erst die Auswahl in W2-T07; hier wird nur die
        /// vollständige Asset-Liste befüllt.</summary>
        public static ReleaseInfo? TryParse(JsonElement release, string sourceName)
        {
            if (!release.TryGetProperty("tag_name", out var tagProp))
                return null;

            if (!AppVersion.TryParse(tagProp.GetString(), out var version))
                return null;

            string? changelog = release.TryGetProperty("body", out var bodyProp)
                ? bodyProp.GetString()
                : null;

            return new ReleaseInfo(version, null, changelog, null, null, sourceName, ExtractAssets(release));
        }

        private static IReadOnlyList<ReleaseAsset> ExtractAssets(JsonElement release)
        {
            if (!release.TryGetProperty("assets", out var assetsProp) || assetsProp.ValueKind != JsonValueKind.Array)
                return [];

            var assets = new List<ReleaseAsset>();
            foreach (var asset in assetsProp.EnumerateArray())
            {
                string? name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                string? url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                long size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out long value) ? value : 0;

                if (name != null && url != null)
                    assets.Add(new ReleaseAsset(name, url, size));
            }

            return assets;
        }
    }
}
