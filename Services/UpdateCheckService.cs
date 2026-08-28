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
    /// Ob ein Update tatsächlich angeboten wird, entscheidet der Aufrufer (ab W2-T09
    /// <c>UpdateDecision.ShouldOffer</c>).</summary>
    internal sealed record UpdateCheckResult(ReleaseInfo? Info, bool FromCache, bool UpdateAvailable);

    /// <summary>
    /// Prüft höchstens alle sechs Stunden auf eine neuere MortysDLP-Version — alles dazwischen
    /// kommt aus dem Zwischenspeicher, ohne Netzzugriff. Ersetzt den bisherigen direkten
    /// <see cref="UpdateService"/>-Aufruf im Startpfad und den toten,
    /// nie instanziierten <c>StartupHealthCheckService</c> (Befund U-05). Siehe
    /// <c>werkstatt/tasks/W2-T06.md</c>.
    /// </summary>
    internal sealed class UpdateCheckService(IReleaseResolver resolver, UpdateCache cache, Func<DateTimeOffset> now)
    {
        private static readonly TimeSpan AppCacheLifetime = TimeSpan.FromHours(6);

        private const string AppCacheKey = "app";
        private const string AppOwner = "MortysTerminal";
        private const string AppRepo = "MortysDLP";

        // Deterministische GitHub-Konvention, siehe 04-UPDATE-ARCHITEKTUR.md Abschnitt 4.
        // Übergangslösung, bis W2-T07 Assets nach Namensmuster auswählt: Für die beiden
        // GitHub-API-Quellen bleibt ReleaseInfo.DownloadUrl bewusst null (Assets werden dort
        // nur befüllt, nicht ausgewertet) - ohne diese Vorlage hätte "Jetzt aktualisieren" im
        // Normalfall (API antwortet zuerst) nichts zum Herunterladen.
        private const string AppDownloadUrlTemplate =
            "https://github.com/{owner}/{repo}/releases/download/{tag}/MortysDLP.zip";

        public async Task<UpdateCheckResult> CheckAppAsync(bool force, CancellationToken ct)
        {
            if (AppInfo.CurrentVersion is not { } current)
            {
                Log.Error("Eigene Version nicht ermittelbar - Update-Prüfung übersprungen.");
                return new UpdateCheckResult(null, FromCache: false, UpdateAvailable: false);
            }

            DateTimeOffset nowValue = now();
            var entry = await cache.ReadAsync(AppCacheKey, ct);

            if (entry != null && !force)
            {
                if (entry.CheckedUtc > nowValue)
                {
                    Log.Warn($"Update-Zwischenspeicher hat einen Zeitstempel aus der Zukunft " +
                        $"({entry.CheckedUtc:u}) - Eintrag gilt als abgelaufen.");
                }
                else if (nowValue - entry.CheckedUtc < AppCacheLifetime)
                {
                    var cachedInfo = ToReleaseInfo(entry);
                    Log.Info($"Update-Prüfung: {Describe(cachedInfo)} " +
                        $"(Quelle {entry.Source}, aus Cache, geprüft vor {FormatAge(nowValue - entry.CheckedUtc)})");
                    return BuildResult(cachedInfo, current, fromCache: true);
                }
            }

            var query = new ReleaseQuery(AppOwner, AppRepo,
                DownloadUrlTemplate: AppDownloadUrlTemplate, ETag: entry?.ETag);

            ReleaseInfo? resolved = await resolver.ResolveAsync(query, current, ct);

            if (resolved is { NotModified: true } && entry != null)
            {
                entry.CheckedUtc = nowValue;
                await cache.WriteAsync(AppCacheKey, entry, ct);

                var cachedInfo = ToReleaseInfo(entry);
                Log.Info($"Update-Prüfung: {Describe(cachedInfo)} " +
                    $"(Quelle {entry.Source}, unverändert bestätigt - 304)");
                return BuildResult(cachedInfo, current, fromCache: true);
            }

            if (resolved is not null)
            {
                // Übergangslösung siehe AppDownloadUrlTemplate oben.
                string? downloadUrl = resolved.DownloadUrl ?? query.ResolveDownloadUrl(resolved.Version.ToString());
                resolved = resolved with { DownloadUrl = downloadUrl };

                await cache.WriteAsync(AppCacheKey, new UpdateCacheEntry
                {
                    CheckedUtc = nowValue,
                    Version = resolved.Version.ToString(),
                    DownloadUrl = resolved.DownloadUrl,
                    Changelog = resolved.Changelog,
                    ETag = resolved.ETag,
                    Source = resolved.SourceName,
                }, ct);

                Log.Info($"Update-Prüfung: {resolved.Version} (Quelle {resolved.SourceName})");
                return BuildResult(resolved, current, fromCache: false);
            }

            if (entry != null)
            {
                var staleInfo = ToReleaseInfo(entry);
                Log.Warn($"Update-Prüfung fehlgeschlagen - letzter bekannter Stand wird verwendet: " +
                    $"{Describe(staleInfo)} (Quelle {entry.Source}).");
                return BuildResult(staleInfo, current, fromCache: true);
            }

            Log.Warn("Update-Prüfung fehlgeschlagen, kein zwischengespeicherter Stand vorhanden.");
            return new UpdateCheckResult(null, FromCache: false, UpdateAvailable: false);
        }

        private static string Describe(ReleaseInfo? info) => info?.Version.ToString() ?? "unbekannt";

        private static string FormatAge(TimeSpan age)
        {
            if (age.TotalHours >= 1) return $"{(int)age.TotalHours} h";
            if (age.TotalMinutes >= 1) return $"{(int)age.TotalMinutes} min";
            return "< 1 min";
        }

        private static UpdateCheckResult BuildResult(ReleaseInfo? info, AppVersion current, bool fromCache)
        {
            bool available = info != null && info.Version > current;
            return new UpdateCheckResult(info, fromCache, available);
        }

        private static ReleaseInfo? ToReleaseInfo(UpdateCacheEntry? entry)
        {
            if (entry?.Version is null || !AppVersion.TryParse(entry.Version, out var version))
                return null;

            return new ReleaseInfo(version, entry.DownloadUrl, entry.Changelog, null, null,
                entry.Source ?? "cache", [], entry.ETag);
        }
    }
}
