using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services.Releases;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services
{
    /// <summary>Ergebnis einer Prüfung. Stellt nur den SACHVERHALT fest ("es gibt etwas
    /// Neueres") — <see cref="UpdateAvailable"/> wertet bewusst kein <c>VersionSkip</c> aus.
    /// Ob ein Update tatsächlich angeboten wird, entscheidet der Aufrufer
    /// (<c>UpdateDecision.ShouldOffer</c>).</summary>
    internal sealed record ToolCheckResult(ReleaseInfo? Info, bool FromCache, bool UpdateAvailable);

    /// <summary>
    /// Prüft auf eine neuere Version eines beliebigen "Prüflings" — Schlüssel, Anfrage,
    /// Gültigkeitsdauer und die laufende Version kommen vollständig herein. Verallgemeinerte
    /// Fassung des früheren <c>UpdateCheckService</c>, der fest auf die App selbst verdrahtet
    /// war (eigener Cache-Schlüssel, eigenes Repository, eigene Download-URL-Vorlage,
    /// <c>AppInfo.CurrentVersion</c>). Die App ist jetzt nur noch ein Aufrufer unter mehreren,
    /// mit dem Schlüssel <c>"app"</c> — zwei fast gleiche Klassen (eine für die App, eine für
    /// Werkzeuge) würden garantiert auseinanderdriften.
    /// </summary>
    internal sealed class ToolCheckService(IReleaseResolver resolver, UpdateCache cache, Func<DateTimeOffset> now)
    {
        /// <summary>Empfohlene Gültigkeitsdauer für die App selbst.</summary>
        public static readonly TimeSpan AppCacheLifetime = TimeSpan.FromHours(6);

        /// <summary>Empfohlene Gültigkeitsdauer für Werkzeuge (Entwurf, Abschnitt zur
        /// Werkzeug-Aktualisierung) — großzügiger als die App: Mit fünf Prüflingen statt einem
        /// ist das GitHub-Kontingent von 60 Anfragen pro Stunde deutlich schneller erschöpft,
        /// und ein Werkzeug-Update eilt seltener als ein Sicherheits-Fix der App selbst.</summary>
        public static readonly TimeSpan ToolCacheLifetime = TimeSpan.FromHours(12);

        /// <param name="cacheKey">Schlüssel im gemeinsamen <see cref="UpdateCache"/>, z. B.
        /// <c>"app"</c> oder <c>"yt-dlp"</c>.</param>
        /// <param name="query">Vollständig vorbereitete Anfrage — <see cref="ReleaseQuery.ETag"/>
        /// wird von dieser Methode selbst aus dem Zwischenspeicher-Eintrag ergänzt, alles
        /// andere (Owner/Repo, Paketname, Text-URL, Download-Vorlage) muss der Aufrufer bereits
        /// gesetzt haben.</param>
        /// <param name="currentVersion">Laufende bzw. installierte Version des Prüflings.
        /// <c>null</c>, wenn sie unbekannt ist (z. B. Werkzeug noch nicht installiert) — dann
        /// gilt jede gefundene Version als "verfügbar", weil sich "nicht neuer" ohne
        /// Vergleichswert nicht behaupten lässt.</param>
        /// <param name="cacheLifetime">Wie lange ein Zwischenspeicher-Eintrag als frisch gilt —
        /// <see cref="AppCacheLifetime"/> oder <see cref="ToolCacheLifetime"/>.</param>
        public async Task<ToolCheckResult> CheckAsync(
            string cacheKey, ReleaseQuery query, AppVersion? currentVersion, TimeSpan cacheLifetime,
            bool force, CancellationToken ct)
        {
            DateTimeOffset nowValue = now();
            var entry = await cache.ReadAsync(cacheKey, ct);

            if (entry != null && !force)
            {
                if (entry.CheckedUtc > nowValue)
                {
                    Log.Warn($"'{cacheKey}': Zwischenspeicher hat einen Zeitstempel aus der " +
                        $"Zukunft ({entry.CheckedUtc:u}) - Eintrag gilt als abgelaufen.");
                }
                else if (nowValue - entry.CheckedUtc < cacheLifetime)
                {
                    var cachedInfo = ToReleaseInfo(entry);
                    Log.Info($"Prüfung '{cacheKey}': {Describe(cachedInfo)} " +
                        $"(Quelle {entry.Source}, aus Cache, geprüft vor {FormatAge(nowValue - entry.CheckedUtc)})");
                    return BuildResult(cachedInfo, currentVersion, fromCache: true);
                }
            }

            GitHubRateLimit.RecordQuery(cacheKey, nowValue);

            var queryWithETag = query with { ETag = entry?.ETag };
            ReleaseInfo? resolved = await resolver.ResolveAsync(queryWithETag, currentVersion, ct);

            if (resolved is { NotModified: true } && entry != null)
            {
                entry.CheckedUtc = nowValue;
                await cache.WriteAsync(cacheKey, entry, ct);

                var cachedInfo = ToReleaseInfo(entry);
                Log.Info($"Prüfung '{cacheKey}': {Describe(cachedInfo)} " +
                    $"(Quelle {entry.Source}, unverändert bestätigt - 304)");
                return BuildResult(cachedInfo, currentVersion, fromCache: true);
            }

            if (resolved is not null)
            {
                // Übergangslösung wie zuvor in UpdateCheckService: Für Quellen ohne eigene
                // Asset-Liste (Atom-Feed, Weiterleitung) liefert ReleaseQuery.ResolveDownloadUrl
                // die Adresse aus der Vorlage.
                string? downloadUrl = resolved.DownloadUrl ?? query.ResolveDownloadUrl(resolved.Version.ToString());
                resolved = resolved with { DownloadUrl = downloadUrl };

                await cache.WriteAsync(cacheKey, new UpdateCacheEntry
                {
                    CheckedUtc = nowValue,
                    Version = resolved.Version.ToString(),
                    DownloadUrl = resolved.DownloadUrl,
                    Changelog = resolved.Changelog,
                    ETag = resolved.ETag,
                    Source = resolved.SourceName,
                    Sha256 = resolved.Sha256,
                    ExpectedSize = resolved.ExpectedSize,
                }, ct);

                Log.Info($"Prüfung '{cacheKey}': {resolved.Version} (Quelle {resolved.SourceName})");
                return BuildResult(resolved, currentVersion, fromCache: false);
            }

            if (entry != null)
            {
                var staleInfo = ToReleaseInfo(entry);
                Log.Warn($"Prüfung '{cacheKey}' fehlgeschlagen - letzter bekannter Stand wird " +
                    $"verwendet: {Describe(staleInfo)} (Quelle {entry.Source}).");
                return BuildResult(staleInfo, currentVersion, fromCache: true);
            }

            Log.Warn($"Prüfung '{cacheKey}' fehlgeschlagen, kein zwischengespeicherter Stand vorhanden.");
            return new ToolCheckResult(null, FromCache: false, UpdateAvailable: false);
        }

        private static string Describe(ReleaseInfo? info) => info?.Version.ToString() ?? "unbekannt";

        private static string FormatAge(TimeSpan age)
        {
            if (age.TotalHours >= 1) return $"{(int)age.TotalHours} h";
            if (age.TotalMinutes >= 1) return $"{(int)age.TotalMinutes} min";
            return "< 1 min";
        }

        private static ToolCheckResult BuildResult(ReleaseInfo? info, AppVersion? current, bool fromCache)
        {
            bool available = info != null && (current is not { } currentVersion || info.Version > currentVersion);
            return new ToolCheckResult(info, fromCache, available);
        }

        private static ReleaseInfo? ToReleaseInfo(UpdateCacheEntry? entry)
        {
            if (entry?.Version is null || !AppVersion.TryParse(entry.Version, out var version))
                return null;

            return new ReleaseInfo(version, entry.DownloadUrl, entry.Changelog, entry.ExpectedSize,
                entry.Sha256, entry.Source ?? "cache", [], entry.ETag);
        }
    }
}
