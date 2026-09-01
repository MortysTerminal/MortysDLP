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
        /// whisper.cpp. <b>Bewusst ohne</b> <see cref="GitHubAtomFeedSource"/> — anders als bei
        /// yt-dlp und TwitchDownloaderCLI trägt dieses Repository <b>zwei</b> Tag-Schemata
        /// parallel: <c>vX.Y.Z</c>-Tags ohne jeden Anhang (semantische Marker, am 2026-09-01
        /// gegen die echte API geprüft: <c>v1.9.3</c> ist <c>prerelease: true</c> und hat
        /// <b>keine</b> Assets) und <c>bNNNN</c>-Tags mit den tatsächlichen Programmpaketen.
        /// <c>GitHubAtomFeedSource</c> kennt keine Vorabversions-Kennzeichnung (der Atom-Feed
        /// führt dieses Feld nicht) und griff im echten Testlauf genau deshalb den assetlosen
        /// <c>v1.9.3</c>-Eintrag — mit der Folge, dass <see cref="ReleaseQueryExtensions.ResolveDownloadUrl"/>
        /// eine Adresse baute, die mit <c>404</c> antwortet, obwohl die versionslose
        /// Rückfalladresse (<see cref="WhisperTool"/>) zuverlässig funktioniert hätte. Eine Quelle,
        /// die eine <b>falsche</b> Adresse liefert, ist schädlicher als eine, die schweigt — deshalb
        /// bleibt sie hier draußen, nicht aus Bequemlichkeit.
        ///
        /// <para><see cref="GitHubApiLatestSource"/> und <see cref="GitHubRedirectSource"/> fragen
        /// beide ausschließlich <c>/releases/latest</c> — dort filtert GitHub selbst Vorabversionen
        /// heraus, das Problem der Atom-Quelle betrifft sie nicht. <b>Am 2026-09-01 gegen die echte
        /// API geprüft:</b> Das Repository heißt inzwischen <c>ggml-org/whisper.cpp</c> —
        /// <c>ggerganov/whisper.cpp</c> ist nur noch eine GitHub-Weiterleitung (die
        /// <c>api.github.com</c> transparent mitgeht, die HEAD-Weiterleitung von
        /// <see cref="GitHubRedirectSource"/> aber nicht, weil dort der erste Sprung die
        /// Weiterleitung auf das neue Owner/Repo wäre, nicht mehr auf den Tag). Der alte Name ist
        /// deshalb keine Option mehr, nicht nur eine veraltete Schreibweise.</para>
        ///
        /// <para><b>Beide verbleibenden Quellen liefern trotzdem heute keine Version:</b>
        /// <c>/releases/latest</c> zeigt aktuell auf den Tag <c>b4938</c> — eine fortlaufende
        /// Build-Nummer, kein <c>vX.Y.Z</c>. <see cref="AppVersion.TryParse"/> scheitert daran, und
        /// <see cref="GitHubApiReleaseJson.TryParse"/> verwirft dabei auch die mitgelieferte
        /// Asset-Liste. Die Ursache liegt im gemeinsamen Quellen-Layer (<see cref="ReleaseInfo.Version"/> ist eine
        /// <see cref="AppVersion"/>) und betrifft potenziell jede weitere Quelle mit
        /// nicht-semantischem Tag-Schema. Beide Quellen bleiben trotzdem stehen: harmlos im
        /// heutigen Fehlschlag, und sofort nutzbar, falls <c>ggml-org</c> seine „latest"-Kennzeichnung
        /// einmal auf ein parsebares Schema umstellt. Die Installation bleibt in der Zwischenzeit
        /// möglich: <see cref="WhisperTool"/> lädt ohne Versionsantwort über eine feste,
        /// versionslose Adresse (wie schon bei yt-dlp und ffmpeg) — am 2026-09-01 gegen das echte
        /// Release durchgespielt.</para>
        /// </summary>
        public static IReadOnlyList<IReleaseSource> CreateWhisperChain() =>
        [
            new GitHubApiLatestSource(),
            new GitHubRedirectSource(),
        ];

        /// <summary>
        /// TwitchDownloaderCLI. Dieselben drei GitHub-Wege wie bei whisper.cpp — anders als dort
        /// ist der Tag von <c>lay295/TwitchDownloader</c> (z. B. <c>1.56.5</c>) eine echte,
        /// ordnende Version, am 2026-09-01 gegen die echte API bestätigt. Die Kette trägt hier
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
