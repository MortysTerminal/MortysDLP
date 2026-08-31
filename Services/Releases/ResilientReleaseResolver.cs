using MortysDLP.Helpers;
using MortysDLP.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Fragt seine Quellen der Reihe nach ab — bewusst seriell, nicht parallel: Der Normalfall
    /// (die erste, primäre Quelle antwortet) soll genau eine Anfrage kosten, nicht vier.
    /// Reihenfolge und Auswahl der Quellen kommen vollständig über den Konstruktor herein —
    /// diese Klasse kennt weder MortysDLP noch ein bestimmtes Repository (→ <c>ReleaseSources</c>
    /// für die App-Kette, Welle 4 für weitere).
    ///
    /// Die einzige Ausnahme von „erste brauchbare Antwort gewinnt" ist die Regel gegen
    /// veraltete Antworten: Eine nicht-primäre Quelle (<see cref="IReleaseSource.IsAuthoritative"/> ==
    /// false), die eine Version meldet, die nicht neuer als <c>current</c> ist, kann das nicht
    /// abschließend beurteilen — Atom-Feeds und Weiterleitungen liegen hinter einem CDN und
    /// können hinterherhinken. Ihre Antwort wird zurückgehalten und die Kette fragt weiter;
    /// antworten am Ende alle Quellen so, gewinnt die höchste der zurückgehaltenen Antworten,
    /// nicht <c>null</c>. Eine primäre Quelle beendet die Kette dagegen immer sofort, auch mit
    /// „gleich oder älter" — der Normalfall (API antwortet, kein Update) darf keine
    /// zusätzliche Anfrage kosten. Ist <c>current</c> <c>null</c>, greift die Regel nicht.
    ///
    /// Meldet eine Quelle <see cref="ReleaseInfo.NotModified"/> (<c>304</c> auf ein
    /// mitgeschicktes <c>ETag</c>), wird das unabhängig von allem anderen sofort
    /// zurückgegeben — der Rückgabewert trägt dann keinen brauchbaren Versionswert, sondern
    /// ist ausschließlich die Bestätigung „unverändert" für den Aufrufer, der den
    /// zugehörigen Zwischenspeicher-Eintrag kennt.
    /// </summary>
    internal sealed class ResilientReleaseResolver(
        IReadOnlyList<IReleaseSource> sources,
        TimeSpan? perSourceTimeout = null) : IReleaseResolver
    {
        private static readonly TimeSpan DefaultPerSourceTimeout = TimeSpan.FromSeconds(8);

        private readonly TimeSpan _perSourceTimeout = perSourceTimeout ?? DefaultPerSourceTimeout;

        public async Task<ReleaseInfo?> ResolveAsync(ReleaseQuery query, AppVersion? current, CancellationToken ct)
        {
            ReleaseInfo? bestStale = null;

            foreach (var source in sources)
            {
                ct.ThrowIfCancellationRequested();

                if (IsGitHubApiSource(source) && GitHubRateLimit.IsExhausted(DateTimeOffset.UtcNow))
                {
                    Log.Info($"'{source.Name}' übersprungen: GitHub-Kontingent erschöpft.");
                    continue;
                }

                ReleaseInfo? info;
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(_perSourceTimeout);

                    try
                    {
                        info = await source.TryGetLatestAsync(query, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Abbruch durch den Nutzer beendet die Kette sofort — er darf nicht als
                        // "Quelle ausgefallen" gelten. Nur wenn NUR das Quellen-Zeitlimit
                        // ausgelöst hat (ct selbst läuft weiter), wird die nächste Quelle
                        // gefragt.
                        if (ct.IsCancellationRequested)
                            throw;

                        Log.Warn($"Quelle '{source.Name}' hat das Zeitlimit " +
                            $"({_perSourceTimeout.TotalSeconds:0} s) überschritten.");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"Quelle '{source.Name}' fehlgeschlagen: {ex.Message}", ex);
                        continue;
                    }
                }

                if (info is null)
                {
                    Log.Warn($"Quelle '{source.Name}' lieferte kein Ergebnis.");
                    continue;
                }

                if (info.NotModified)
                {
                    // 304: bestätigt nur, dass sich seit dem mitgeschickten ETag
                    // nichts geändert hat - kein echter Versionswert, also nie über die
                    // Regel gegen veraltete Antworten laufen lassen. Der Aufrufer
                    // (UpdateCheckService) kennt den zugehörigen Zwischenspeicher-Eintrag und
                    // bestätigt ihn selbst.
                    Log.Info($"Quelle '{source.Name}' meldet: unverändert (304).");
                    return info;
                }

                if (!source.IsAuthoritative && current is { } currentVersion && info.Version <= currentVersion)
                {
                    Log.Info($"Quelle '{source.Name}' meldet {info.Version} - nicht neuer als die " +
                        "laufende Version, gilt als nicht abschließend.");

                    if (bestStale is null || info.Version > bestStale.Version)
                        bestStale = info;

                    continue;
                }

                Log.Info($"Version von '{source.Name}': {info.Version}");
                return info;
            }

            return bestStale;
        }

        private static bool IsGitHubApiSource(IReleaseSource source) =>
            source is GitHubApiLatestSource or GitHubApiListSource;
    }
}
