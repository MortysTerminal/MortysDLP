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
    /// bestimmte Reihenfolge — die Kette dafür ist <c>ResilientReleaseResolver</c>.
    ///
    /// </summary>
    internal interface IReleaseSource
    {
        /// <summary>Für Protokoll und Diagnose, z. B. "github-api-latest".</summary>
        string Name { get; }

        /// <summary>true nur für Quellen, deren Antwort abschließend ist (die beiden
        /// GitHub-API-Quellen). CDN- und handgepflegte Quellen können veraltet sein — die Regel
        /// dahinter (eine kleinere/gleiche Version einer nicht-abschließenden Quelle ist kein
        /// Beweis für "kein Update") wird erst ausgewertet.</summary>
        bool IsAuthoritative { get; }

        /// <summary>Liefert die neueste Version oder <c>null</c>, wenn diese Quelle keine
        /// brauchbare Antwort hat — wirft nicht, außer bei Abbruch über <paramref name="ct"/>.</summary>
        Task<ReleaseInfo?> TryGetLatestAsync(ReleaseQuery query, CancellationToken ct);
    }

    /// <summary>Beschreibt, wonach gefragt wird. Repository-Spezifisches (Owner/Repo,
    /// Namensmuster, URL-Vorlage) kommt ausschließlich hierüber herein — keine Quelle darf
    /// eigene Annahmen über ein bestimmtes Repository treffen, sonst lässt sie sich in
    /// Welle 4 nicht für die anderen Werkzeuge wiederverwenden.
    ///
    /// <para><c>Owner</c>/<c>Repo</c> sind ein GitHub-Detail, historisch der einzige Kern
    /// dieses Typs — sie bleiben Pflichtfelder, weil die vier GitHub-Quellen sie brauchen.
    /// <c>PackageName</c> und <c>PlainTextVersionUrl</c> ergänzen das um die beiden
    /// GitHub-unabhängigen Fälle: ein PyPI-Paketname ist keine URL, eine reine
    /// Text-Antwort-URL (z. B. gyan.dev) ist kein Paketname und folgt keinem gemeinsamen
    /// Muster mehrerer Anbieter. Eine Quelle liest ausschließlich das Feld, das sie
    /// versteht, und ignoriert den Rest — eine <see cref="ResilientReleaseResolver"/>-Kette
    /// kann trotzdem GitHub- und Nicht-GitHub-Quellen mischen, weil alle dieselbe Anfrage
    /// bekommen.</para></summary>
    /// <param name="ETag">Zuletzt bekannter <c>ETag</c> dieser Abfrage. Gesetzt, senden
    /// die beiden GitHub-API-Quellen <c>If-None-Match</c> — eine Bestätigung per <c>304</c>
    /// kostet kein Kontingent.</param>
    /// <param name="PackageName">Paketname für <see cref="PyPiReleaseSource"/>, z. B.
    /// <c>"yt-dlp"</c>. <c>null</c>, wenn die Kette keine PyPI-Quelle enthält.</param>
    /// <param name="PlainTextVersionUrl">Vollständige URL für <see cref="PlainTextVersionSource"/>,
    /// deren Antwort ausschließlich aus einer Versionsnummer besteht (z. B.
    /// <c>https://www.gyan.dev/ffmpeg/builds/release-version</c>). Anders als bei
    /// <c>PackageName</c> gibt es hier kein gemeinsames URL-Muster mehrerer Anbieter — die
    /// Quelle kennt nur die fertige Adresse, nie einen Anbieter.</param>
    internal sealed record ReleaseQuery(
        string Owner,
        string Repo,
        string? AssetPattern = null,
        string? DownloadUrlTemplate = null,
        bool AllowPrerelease = false,
        string? ETag = null,
        string? PackageName = null,
        string? PlainTextVersionUrl = null);

    /// <summary>Ergebnis einer Quelle. Führt bewusst nur die geparste <see cref="AppVersion"/>,
    /// keine Roh-Zeichenkette des Tags — jede Quelle schreibt den Tag anders,
    /// die Anzeige darf sich nicht danach richten, welche Quelle geantwortet hat. Wer anzeigt,
    /// protokolliert oder speichert, nimmt <c>Version.ToString()</c>.</summary>
    /// <param name="ETag"><c>ETag</c> der Antwort, zum Zwischenspeichern. <c>null</c>,
    /// wenn die Quelle keine Kopfzeile mitschickt.</param>
    /// <param name="NotModified">true nur, wenn diese Antwort aus einem <c>304 Not Modified</c>
    /// stammt — dann sind alle anderen Felder außer <see cref="ETag"/> und
    /// <see cref="SourceName"/> bedeutungslos. Der Aufrufer (<c>ToolCheckService</c>)
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
    /// nach Namensmuster und Prüfsumme sind nicht Teil dieser Aufgabe — hier wird
    /// nur befüllt, nicht ausgewertet.</summary>
    internal sealed record ReleaseAsset(string Name, string Url, long Size);

    /// <summary>Metadatenantworten jenseits eines vernünftigen Maßes werden verworfen statt
    /// gelesen — ein GitHub-JSON oder ein Atom-Feed
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
