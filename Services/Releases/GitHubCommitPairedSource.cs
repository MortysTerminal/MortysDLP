using MortysDLP.Helpers;
using MortysDLP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Quelle für Repositories, die Versionsnummer und Programmpaket auf <b>zwei</b> Releases
    /// verteilen: ein stabiles <c>vX.Y.Z</c>-Release ohne Anhänge und ein Build-Release (z. B.
    /// <c>b5454</c>, oft als Vorabversion markiert) mit den Paketen. Beide zeigen auf denselben
    /// Commit (<c>target_commitish</c>) — darüber werden sie zusammengeführt. Trägt das
    /// Versions-Release die Anhänge noch selbst (älteres Schema), wird es direkt genommen.
    ///
    /// <para>Liest <c>GET /repos/{owner}/{repo}/releases?per_page=30</c> und geht die stabilen,
    /// parsebaren Versionen absteigend durch; die erste, zu der ein Paket nach
    /// <see cref="ReleaseQuery.AssetPattern"/> existiert, gewinnt. Die Version stammt immer aus dem
    /// Versions-Tag, Adresse, Größe und Prüfsumme (<c>digest</c>) aus dem Paket-Anhang. Lässt sich
    /// nichts eindeutig zuordnen, liefert die Quelle <c>null</c> statt einer geratenen Version.</para>
    ///
    /// <para>Zugeordnet wird nur über einen vollständigen 40-stelligen Commit-Hash: Ältere
    /// Releases tragen dort einen Branch-Namen (<c>master</c>), und zwei Releases mit demselben
    /// Branch-Namen haben nichts miteinander zu tun.</para>
    /// </summary>
    internal sealed partial class GitHubCommitPairedSource(HttpClient? client = null) : IReleaseSource
    {
        private const int PageSize = 30;

        private readonly HttpClient _client = client ?? Http.Shared;

        public string Name => "github-api-commit-paired";

        public bool IsAuthoritative => true;

        public async Task<ReleaseInfo?> TryGetLatestAsync(ReleaseQuery query, CancellationToken ct)
        {
            if (GitHubRateLimit.IsExhausted(DateTimeOffset.UtcNow))
                return null;

            string url = $"https://api.github.com/repos/{query.Owner}/{query.Repo}/releases?per_page={PageSize}";

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
                return ParsePairedRelease(json, query, response.Headers.ETag?.Tag);
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

        internal ReleaseInfo? ParsePairedRelease(string json, ReleaseQuery query, string? etag = null)
        {
            if (string.IsNullOrEmpty(query.AssetPattern))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return null;

                var releases = doc.RootElement.EnumerateArray()
                    .Where(r => r.ValueKind == JsonValueKind.Object && !IsDraft(r))
                    .ToList();

                var versions = releases
                    .Where(r => GitHubApiReleaseJson.IsEligible(r, query))
                    .Select(r => (Release: r, Version: TryReadVersion(r)))
                    .Where(x => x.Version.HasValue)
                    .OrderByDescending(x => x.Version!.Value)
                    .ToList();

                foreach (var (versionRelease, version) in versions)
                {
                    if (TrySelectAsset(versionRelease, query.AssetPattern) is { } own)
                        return CreateInfo(version!.Value, versionRelease, own, etag);

                    string? commit = ReadCommitHash(versionRelease);
                    if (commit is null)
                        continue;

                    foreach (var build in releases)
                    {
                        // Das Versions-Release selbst ist oben schon geprüft.
                        if (build.Equals(versionRelease))
                            continue;

                        if (string.Equals(ReadCommitHash(build), commit, StringComparison.OrdinalIgnoreCase) &&
                            TrySelectAsset(build, query.AssetPattern) is { } paired)
                        {
                            return CreateInfo(version!.Value, versionRelease, paired, etag);
                        }
                    }
                }

                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Changelog aus dem Versions-Release (dort steht der Text, am Build meist nur eine
        /// Commit-Liste). Der gewählte Anhang wird zusätzlich als <see cref="ReleaseInfo.Assets"/>
        /// mitgegeben, damit ein Aufrufer, der Anhänge erwartet, nicht erneut fragen muss.</summary>
        private ReleaseInfo CreateInfo(AppVersion version, JsonElement versionRelease, PairedAsset asset, string? etag)
        {
            string? changelog = versionRelease.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
                ? body.GetString()
                : null;

            return new(version, asset.Url, changelog, asset.Size > 0 ? asset.Size : null, asset.Sha256, Name,
                [asset.Asset], etag);
        }

        private static bool IsDraft(JsonElement release) =>
            release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True;

        private static AppVersion? TryReadVersion(JsonElement release) =>
            release.TryGetProperty("tag_name", out var tag) &&
            tag.ValueKind == JsonValueKind.String &&
            AppVersion.TryParse(tag.GetString(), out var version)
                ? version
                : null;

        private static string? ReadCommitHash(JsonElement release)
        {
            if (!release.TryGetProperty("target_commitish", out var prop) || prop.ValueKind != JsonValueKind.String)
                return null;

            string? value = prop.GetString();
            return value is not null && CommitHashRegex().IsMatch(value) ? value : null;
        }

        private PairedAsset? TrySelectAsset(JsonElement release, string pattern)
        {
            if (!release.TryGetProperty("assets", out var assetsProp) || assetsProp.ValueKind != JsonValueKind.Array)
                return null;

            var assets = new List<(ReleaseAsset Asset, string? Sha256)>();
            foreach (var asset in assetsProp.EnumerateArray())
            {
                string? name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                string? url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                long size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out long value) ? value : 0;
                string? digest = asset.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String
                    ? d.GetString()
                    : null;

                if (name != null && url != null)
                    assets.Add((new ReleaseAsset(name, url, size), ParseSha256Digest(digest)));
            }

            if (assets.Count == 0)
                return null;

            try
            {
                var selected = AssetSelector.Select([.. assets.Select(a => a.Asset)], pattern);
                if (selected is null)
                    return null;

                string? sha256 = assets.First(a => ReferenceEquals(a.Asset, selected)).Sha256;
                return new PairedAsset(selected, sha256);
            }
            catch (AssetAmbiguousException ex)
            {
                Log.Warn($"{Name}: Anhang nicht eindeutig wählbar: {ex.Message}");
                return null;
            }
        }

        /// <summary>GitHub meldet je Anhang <c>digest: "sha256:&lt;64 hex&gt;"</c>. Alles andere
        /// (anderer Algorithmus, falsche Länge, kein Präfix) ergibt keine Prüfsumme — eine
        /// fehlende Prüfsumme lädt mit Größenprüfung, eine falsch gelesene würde den Download
        /// grundlos ablehnen.</summary>
        internal static string? ParseSha256Digest(string? digest)
        {
            const string Prefix = "sha256:";
            if (digest is null || !digest.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                return null;

            string hex = digest[Prefix.Length..];
            return Sha256HexRegex().IsMatch(hex) ? hex.ToLowerInvariant() : null;
        }

        private static HttpRequestMessage CreateRequest(string url, string? etag)
        {
            var request = Http.CreateGitHubApiRequest(url);
            if (!string.IsNullOrEmpty(etag))
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            return request;
        }

        [GeneratedRegex("^[0-9a-fA-F]{40}$")]
        private static partial Regex CommitHashRegex();

        [GeneratedRegex("^[0-9a-fA-F]{64}$")]
        private static partial Regex Sha256HexRegex();

        private sealed record PairedAsset(ReleaseAsset Asset, string? Sha256)
        {
            public string Url => Asset.Url;

            public long Size => Asset.Size;
        }
    }
}
