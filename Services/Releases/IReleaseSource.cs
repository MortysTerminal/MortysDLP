using MortysDLP.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Eine von mehreren voneinander unabhängigen Quellen für die Frage „welche Version ist
    /// die neueste?". Jede Quelle ist klein, kennt weder die anderen Quellen noch eine
    /// bestimmte Reihenfolge — die Kette dafür ist <c>ResilientReleaseResolver</c> (W2-T04b).
    /// Siehe <c>werkstatt/04-UPDATE-ARCHITEKTUR.md</c>, Abschnitt 4.
    /// </summary>
    internal interface IReleaseSource
    {
        /// <summary>Für Protokoll und Diagnose, z. B. "github-api-latest".</summary>
        string Name { get; }

        /// <summary>true nur für Quellen, deren Antwort abschließend ist (die beiden
        /// GitHub-API-Quellen). CDN- und handgepflegte Quellen können veraltet sein — die Regel
        /// dahinter (eine kleinere/gleiche Version einer nicht-abschließenden Quelle ist kein
        /// Beweis für "kein Update") wird erst in W2-T04b ausgewertet.</summary>
        bool IsAuthoritative { get; }

        /// <summary>Liefert die neueste Version oder <c>null</c>, wenn diese Quelle keine
        /// brauchbare Antwort hat — wirft nicht, außer bei Abbruch über <paramref name="ct"/>.</summary>
        Task<ReleaseInfo?> TryGetLatestAsync(ReleaseQuery query, CancellationToken ct);
    }

    /// <summary>Beschreibt, wonach gefragt wird. Repository-Spezifisches (Owner/Repo,
    /// Namensmuster, URL-Vorlage) kommt ausschließlich hierüber herein — keine Quelle darf
    /// eigene Annahmen über ein bestimmtes Repository treffen, sonst lässt sie sich in
    /// Welle 4 nicht für die anderen Werkzeuge wiederverwenden.</summary>
    /// <param name="ETag">Zuletzt bekannter <c>ETag</c> dieser Abfrage (W2-T06). Gesetzt, senden
    /// die beiden GitHub-API-Quellen <c>If-None-Match</c> — eine Bestätigung per <c>304</c>
    /// kostet kein Kontingent.</param>
    internal sealed record ReleaseQuery(
        string Owner,
        string Repo,
        string? AssetPattern = null,
        string? DownloadUrlTemplate = null,
        bool AllowPrerelease = false,
        string? ETag = null);

    /// <summary>Ergebnis einer Quelle. Führt bewusst nur die geparste <see cref="AppVersion"/>,
    /// keine Roh-Zeichenkette des Tags (Befund U-15) — jede Quelle schreibt den Tag anders,
    /// die Anzeige darf sich nicht danach richten, welche Quelle geantwortet hat. Wer anzeigt,
    /// protokolliert oder speichert, nimmt <c>Version.ToString()</c>.</summary>
    /// <param name="ETag"><c>ETag</c> der Antwort, zum Zwischenspeichern (W2-T06). <c>null</c>,
    /// wenn die Quelle keine Kopfzeile mitschickt.</param>
    /// <param name="NotModified">true nur, wenn diese Antwort aus einem <c>304 Not Modified</c>
    /// stammt — dann sind alle anderen Felder außer <see cref="ETag"/> und
    /// <see cref="SourceName"/> bedeutungslos. Der Aufrufer (<c>UpdateCheckService</c>)
    /// bestätigt in diesem Fall den vorhandenen Zwischenspeicher-Eintrag, statt ihn zu
    /// ersetzen.</param>
    internal sealed record ReleaseInfo(
        AppVersion Version,
        string? DownloadUrl,
        string? Changelog,
        long? ExpectedSize,
        string? Sha256,
        string SourceName,
        IReadOnlyList<ReleaseAsset> Assets,
        string? ETag = null,
        bool NotModified = false);

    /// <summary>Ein Release-Anhang, wie eine Quelle mit Asset-Information ihn kennt. Auswahl
    /// nach Namensmuster und Prüfsumme sind nicht Teil dieser Aufgabe (→ W2-T07) — hier wird
    /// nur befüllt, nicht ausgewertet.</summary>
    internal sealed record ReleaseAsset(string Name, string Url, long Size);

    /// <summary>Metadatenantworten jenseits eines vernünftigen Maßes werden verworfen statt
    /// gelesen (<c>02-BEST-PRACTICES.md</c>, Abschnitt 5) — ein GitHub-JSON oder ein Atom-Feed
    /// ist immer klein; alles jenseits dieser Grenze ist keine Release-Antwort mehr.</summary>
    internal static class ReleaseResponseGuard
    {
        public const long MaxResponseBytes = 5 * 1024 * 1024;

        public static bool ExceedsLimit(HttpResponseMessage response) =>
            response.Content.Headers.ContentLength is long length && length > MaxResponseBytes;
    }

    /// <summary>Löst <see cref="ReleaseQuery.DownloadUrlTemplate"/> auf — für Quellen, die
    /// selbst keine Asset-Liste kennen (Atom-Feed, Weiterleitung). Der eingesetzte Tag ist
    /// bewusst die Rohform, wie die jeweilige Quelle ihn gelesen hat: Die URL muss zum
    /// tatsächlichen Release passen, nur die Anzeige wird normalisiert.</summary>
    internal static class ReleaseQueryExtensions
    {
        public static string? ResolveDownloadUrl(this ReleaseQuery query, string rawTag)
        {
            if (string.IsNullOrEmpty(query.DownloadUrlTemplate))
                return null;

            return query.DownloadUrlTemplate
                .Replace("{owner}", query.Owner, StringComparison.Ordinal)
                .Replace("{repo}", query.Repo, StringComparison.Ordinal)
                .Replace("{tag}", rawTag, StringComparison.Ordinal);
        }
    }
}
