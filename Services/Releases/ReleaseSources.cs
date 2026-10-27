using MortysDLP.Models;
using MortysDLP.Services.Tools;
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

        /// <summary>
        /// whisper.cpp. Nur <see cref="GitHubCommitPairedSource"/>: Seit 2026-09-11 tragen die
        /// stabilen <c>vX.Y.Z</c>-Releases keine Anhänge mehr, die Pakete hängen an
        /// <c>bNNNN</c>-Vorabversionen mit demselben Commit (gegen die echte API geprüft,
        /// 2026-10-26). <c>/releases/latest</c> zeigt damit auf ein Release mit Version, aber ohne
        /// Paket — <see cref="GitHubApiLatestSource"/> würde die Kette mit dieser Antwort beenden,
        /// <see cref="GitHubRedirectSource"/> über die Adressvorlage eine Adresse liefern, die mit
        /// <c>404</c> antwortet. Beide verhinderten so den Rückfall auf die feste Paketadresse in
        /// <see cref="WhisperTool"/> und sind deshalb für dieses Repository draußen.
        /// <see cref="GitHubAtomFeedSource"/> kennt keine Vorabversions-Kennzeichnung und griff schon
        /// beim früheren Schema assetlose Tags.
        ///
        /// <para>Antwortet die Quelle nicht, installiert <see cref="WhisperTool"/> über eine fest
        /// hinterlegte, geprüfte Paketadresse mit Prüfsumme — die Installation hängt also nicht an
        /// dieser einen Anfrage.</para>
        /// </summary>
        public static IReadOnlyList<IReleaseSource> CreateWhisperChain() =>
        [
            new GitHubCommitPairedSource(),
        ];

        /// <summary>
        /// TwitchDownloaderCLI. Die drei klassischen GitHub-Wege (API, Atom, Weiterleitung) — anders
        /// als bei whisper.cpp ist der Tag von <c>lay295/TwitchDownloader</c> (z. B. <c>1.56.5</c>)
        /// eine echte, ordnende Version am selben Release wie das Paket, am 2026-09-01 gegen die
        /// echte API bestätigt. Die Kette trägt hier
        /// also tatsächlich, nicht nur auf dem Papier.
        /// </summary>
        public static IReadOnlyList<IReleaseSource> CreateTwitchDownloaderChain() =>
        [
            new GitHubApiLatestSource(),
            new GitHubAtomFeedSource(),
            new GitHubRedirectSource(),
        ];
    }
}
