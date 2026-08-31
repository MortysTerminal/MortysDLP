using System.Collections.Generic;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Feste Quellenketten je Prüfling. Reihenfolge überall „reichste Information zuerst":
    /// API-Quellen liefern Changelog, Asset-Namen und Größen; ein Atom-Feed liefert Text, aber
    /// keine Assets; eine Weiterleitung oder ein Textendpunkt liefert nur die Versionsnummer.
    /// </summary>
    internal static class ReleaseSources
    {
        /// <summary>
        /// MortysDLP selbst. <c>version.json</c> steht bewusst am Ende: Sie wird von Hand gepflegt
        /// und kann als einzige Quelle dauerhaft falsch sein, wenn diese Pflege vergessen wird —
        /// der Rettungsanker, wenn alle GitHub-Endpunkte schweigen, nicht die erste Anlaufstelle.
        /// </summary>
        public static IReadOnlyList<IReleaseSource> CreateAppChain() =>
        [
            new GitHubApiLatestSource(),
            new GitHubApiListSource(),
            new GitHubAtomFeedSource(),
            new GitHubRedirectSource(),
            new VersionJsonReleaseSource(),
        ];

        /// <summary>
        /// yt-dlp. PyPI steht direkt hinter der GitHub-API und vor den beiden übrigen
        /// GitHub-Wegen, weil es die einzige Quelle der Kette ist, die <b>vollständig ohne
        /// GitHub</b> auskommt: kein gemeinsames Kontingent, keine gemeinsame Infrastruktur. Fällt
        /// GitHub als Ganzes aus, ist sie die einzige, die überhaupt noch antwortet — dafür kennt
        /// sie keine Assets, sodass die Download-Adresse aus
        /// <see cref="ReleaseQuery.DownloadUrlTemplate"/> kommen muss.
        /// </summary>
        public static IReadOnlyList<IReleaseSource> CreateYtDlpChain() =>
        [
            new GitHubApiLatestSource(),
            new PyPiReleaseSource(),
            new GitHubAtomFeedSource(),
            new GitHubRedirectSource(),
        ];

        /// <summary>
        /// ffmpeg/ffprobe — eine einzige Versionsquelle, und das ist keine Nachlässigkeit:
        /// <c>www.gyan.dev/ffmpeg/builds/release-version</c> ist eine winzige Textdatei mit genau
        /// der Versionsnummer des Pakets, das MortysDLP auch herunterlädt. Sie ist deshalb
        /// <see cref="IReleaseSource.IsAuthoritative"/>: Für <i>dieses</i> Paket ist ihre Antwort
        /// abschließend, es gibt keine zweite Stelle, die es besser wüsste.
        ///
        /// <para><b>Warum <c>BtbN/FFmpeg-Builds</c> hier bewusst fehlt</b> — der Entwurf nennt es
        /// als zweite Quelle, gegen den vorhandenen Code geprüft trägt es aber nicht:
        /// Erstens vergeben diese Builds Tags wie <c>latest</c> und
        /// <c>autobuild-2026-08-19-12-55</c>, und <c>ReleaseInfo.Version</c> ist eine
        /// <c>AppVersion</c> — <c>GitHubApiReleaseJson.TryParse</c> verwirft einen solchen Tag und
        /// liefert <c>null</c>. Die Quelle wäre in der Kette, ohne je etwas beizutragen.
        /// Zweitens, und wichtiger: Die Version eines BtbN-Builds sagt nichts über die Version des
        /// gyan.dev-Pakets aus, das installiert ist. Eine Kette, die die Version von einem
        /// Anbieter und das Paket von einem anderen holt, vergleicht Äpfel mit Birnen — und
        /// erzeugt genau das dauerhafte Update-Angebot, das diese Aufgabe verhindern soll. Eine
        /// zweite ffmpeg-Quelle braucht deshalb erst einen Versionsbegriff je Anbieter; das ist
        /// eine eigene Aufgabe und keine Zeile in dieser Liste.</para>
        ///
        /// <para>Der Ausfall dieser Quelle ist trotzdem abgedeckt, nur nicht hier: Die feste
        /// Paketadresse aus <c>Properties/Resources.resx</c> zeigt immer auf die aktuelle Ausgabe.
        /// Ohne Antwort der Versionsquelle kann MortysDLP kein Update <i>anbieten</i> — ein
        /// fehlendes ffmpeg aber jederzeit <i>installieren</i>.</para>
        /// </summary>
        public static IReadOnlyList<IReleaseSource> CreateFfmpegChain() =>
        [
            new PlainTextVersionSource("gyan-dev-release-version", isAuthoritative: true),
        ];
    }
}
