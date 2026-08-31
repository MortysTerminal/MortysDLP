using MortysDLP.Models;
using MortysDLP.Services.Releases;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Tools
{
    /// <summary>
    /// Wie mit einer gefundenen entfernten Version umzugehen ist. Beide Werte führen
    /// ausschließlich zu einem <b>Angebot</b> — ein Werkzeug-Update läuft in keinem Fall ohne
    /// Zutun des Nutzers. Der Unterschied liegt darin, <i>wann</i> überhaupt etwas angeboten wird.
    /// </summary>
    internal enum ToolUpdatePolicy
    {
        /// <summary>Nur anbieten, wenn die entfernte Version nachweislich neuer ist. „Weiß nicht"
        /// bedeutet: kein Angebot. Für Werkzeuge mit ordnender Version (yt-dlp) — verhindert das
        /// Downgrade-Angebot, wenn lokal ein Nightly-Build neuer ist als der letzte Release.</summary>
        OnlyWhenNewer,

        /// <summary>Anbieten, sobald sich die Ausgabe unterscheidet. Für Werkzeuge, deren Version
        /// nicht ordnend ist (ffmpeg): „neuer als" ist dort nicht beantwortbar, „dieselbe Ausgabe
        /// oder nicht" schon. Ein Downgrade wird trotzdem verhindert, soweit die Zahlenkerne das
        /// hergeben.</summary>
        WhenDifferent,
    }

    /// <summary>Zustand eines Werkzeugs, <b>ohne Netzzugriff und ohne Prozessstart</b> ermittelt:
    /// Sind alle Dateien da und größer als 0 Byte? Das beantwortet ausdrücklich <b>nicht</b>, ob
    /// dort auch das richtige Programm liegt — dafür muss es gefragt werden
    /// (<see cref="IManagedTool.ProbeAsync"/>).</summary>
    /// <param name="MissingPaths">Zieldateien, die fehlen oder 0 Byte groß sind. Leer, wenn
    /// <see cref="Installed"/> gilt.</param>
    internal sealed record ToolStatus(
        string ToolId,
        bool Installed,
        IReadOnlyList<string> MissingPaths);

    /// <summary>Wie brauchbar ein Werkzeug tatsächlich ist — beantwortbar erst, nachdem es
    /// gefragt wurde.</summary>
    internal enum ToolHealth
    {
        /// <summary>Vorhanden, antwortet, und die Antwort passt zu diesem Werkzeug.</summary>
        Ok,

        /// <summary>Mindestens eine Zieldatei fehlt oder ist leer.</summary>
        NotInstalled,

        /// <summary>Die Datei ist da, ließ sich aber nicht befragen: Prozess nicht startbar,
        /// Zeitlimit überschritten, Exit-Code ungleich 0.</summary>
        NoAnswer,

        /// <summary>Die Datei ist da und hat geantwortet — aber nicht so, wie dieses Werkzeug
        /// antwortet. Der Fall, der zählt: Eine beliebige umbenannte EXE liegt unter dem Namen
        /// des Werkzeugs. Ein Dateiname ist kein Nachweis.</summary>
        Foreign,
    }

    /// <param name="Version">Gelesene Version. Nur bei <see cref="ToolHealth.Ok"/> verlässlich.</param>
    /// <param name="Answer">Die tatsächliche Antwort des Programms, gekürzt — gehört ins
    /// Protokoll, damit ein „passt nicht" nachvollziehbar ist und nicht geraten werden muss.
    /// <c>null</c>, wenn es nichts geantwortet hat.</param>
    internal sealed record ToolProbe(ToolHealth Health, ToolVersion Version, string? Answer)
    {
        public bool Usable => Health == ToolHealth.Ok;

        public static ToolProbe NotInstalled { get; } =
            new(ToolHealth.NotInstalled, ToolVersion.Unknown, null);
    }

    /// <summary>Abschnitte einer Installation, für die Statuszeile des Aufrufers. Bewusst ein
    /// Aufzählungstyp und kein fertiger Text: Die Übersetzung gehört in die Oberfläche, nicht in
    /// die Werkzeugschicht.</summary>
    internal enum ToolInstallStage
    {
        Downloading,
        Extracting,
        Replacing,
        Verifying,
    }

    /// <summary>Wie eine Installation ausgegangen ist.</summary>
    internal enum ToolInstallStatus
    {
        /// <summary>Neu eingerichtet, vorher war nichts da.</summary>
        Installed,

        /// <summary>Vorhandene Dateien ersetzt, Erfolgskontrolle bestanden.</summary>
        Replaced,

        /// <summary>Ersetzt, aber die Erfolgskontrolle ist durchgefallen — der vorherige Stand
        /// wurde zurückgeholt und ist unverändert einsatzbereit.</summary>
        RolledBack,

        /// <summary>Fehlgeschlagen, bevor etwas ersetzt wurde (kein Download, kein Paketinhalt,
        /// keine Adresse). Die Installation ist unberührt.</summary>
        Failed,

        /// <summary>Vom Nutzer abgebrochen. Wie <see cref="Failed"/> ohne Fehlerfall.</summary>
        Canceled,
    }

    /// <param name="NewVersion">Die nach der Erfolgskontrolle gemessene Version. Nur bei
    /// <see cref="ToolInstallStatus.Installed"/>/<see cref="ToolInstallStatus.Replaced"/>
    /// gesetzt.</param>
    /// <param name="Detail">Ein Satz für Protokoll und Bericht — warum es so ausgegangen ist.</param>
    internal sealed record ToolInstallOutcome(
        ToolInstallStatus Status,
        ToolVersion NewVersion,
        string Detail)
    {
        public bool Success => Status is ToolInstallStatus.Installed or ToolInstallStatus.Replaced;
    }

    /// <param name="RemovedPaths">Tatsächlich gelöschte Dateien.</param>
    /// <param name="FailedPaths">Dateien, die nicht gelöscht werden konnten (z. B. in Benutzung).</param>
    internal sealed record ToolRemovalResult(
        IReadOnlyList<string> RemovedPaths,
        IReadOnlyList<string> FailedPaths)
    {
        public bool Success => FailedPaths.Count == 0;
    }

    /// <summary>
    /// Ein von MortysDLP verwaltetes externes Werkzeug: vorhanden?, welche Version?, gibt es eine
    /// neuere?, installieren, reparieren, deinstallieren.
    ///
    /// <para>Die Abstraktion ist absichtlich an den beiden <b>ungleichsten</b> Werkzeugen
    /// entstanden — yt-dlp (eine EXE, ordnende Version, vier Metadatenquellen, eigener
    /// Selbstaktualisierungs-Notausgang) und ffmpeg (ein ZIP mit zwei Zieldateien, nicht ordnende
    /// Version, ein Textendpunkt als Versionsquelle). Was hier steht, trägt beide; was nur eines
    /// von beiden trägt, steht nicht hier, sondern in der jeweiligen Klasse.</para>
    ///
    /// <para><b>Reparatur</b> ist keine eigene Methode: Sie ist
    /// <see cref="InstallAsync"/> ohne vorherigen Versionsvergleich — dieselben Schritte,
    /// dieselbe Rückfallebene, dieselbe Erfolgskontrolle. Eine zweite Methode, die dasselbe tut,
    /// würde nur auseinanderdriften.</para>
    /// </summary>
    internal interface IManagedTool
    {
        /// <summary>Kurzer, stabiler Bezeichner — gleichzeitig Schlüssel im
        /// <see cref="UpdateCache"/> und Präfix jeder Protokollzeile dieses Werkzeugs. Ändert er
        /// sich, verfällt der Zwischenspeicher-Eintrag; er ist deshalb keine Anzeigebezeichnung.</summary>
        string Id { get; }

        /// <summary>Für Dialoge und Protokoll, z. B. <c>"ffmpeg / ffprobe"</c>.</summary>
        string DisplayName { get; }

        /// <summary>true, wenn MortysDLP ohne dieses Werkzeug nicht arbeiten kann. Steuert nur, wie
        /// dringlich ein <i>fehlendes</i> Werkzeug behandelt wird — auf ein <i>Update</i> hat es
        /// keinen Einfluss (siehe <see cref="ToolUpdatePolicy"/>).</summary>
        bool RequiredForOperation { get; }

        ToolUpdatePolicy UpdatePolicy { get; }

        /// <summary>Alle Dateien, die zu diesem Werkzeug gehören — bei ffmpeg zwei. Fehlt eine
        /// davon, gilt das Werkzeug als nicht installiert: ein ffmpeg ohne ffprobe ist für die
        /// Anwendung dasselbe wie kein ffmpeg.</summary>
        IReadOnlyList<string> TargetPaths { get; }

        /// <summary>Die Quellenkette dieses Werkzeugs, in Abfragereihenfolge.</summary>
        IReadOnlyList<IReleaseSource> CreateSources();

        /// <summary>Die Anfrage, die zu <see cref="CreateSources"/> passt.</summary>
        ReleaseQuery CreateQuery();

        /// <summary>Ohne Netz, ohne Prozessstart: reine Dateiprüfung. Sagt nur, ob etwas
        /// <i>da</i> ist — nicht, ob es das Richtige ist.</summary>
        ToolStatus GetStatus();

        /// <summary>
        /// Fragt das Werkzeug selbst (<c>--version</c> bzw. <c>-version</c>) und beurteilt die
        /// Antwort: fehlt es, schweigt es, oder antwortet dort etwas, das gar nicht dieses
        /// Werkzeug ist? Jeder dieser Fälle wird unterschieden und protokolliert, weil sie
        /// verschiedene Ursachen haben — und weil ein „schweigt" nie als „Update nötig" gelesen
        /// werden darf.
        ///
        /// <para>Der Fall <see cref="ToolHealth.Foreign"/> ist der wichtigste: Eine beliebige EXE,
        /// die auf <c>yt-dlp.exe</c> umbenannt wurde, liegt für <see cref="GetStatus"/>
        /// vollkommen richtig da. Erst die Antwort verrät sie.</para>
        /// </summary>
        Task<ToolProbe> ProbeAsync(CancellationToken ct);

        /// <summary>
        /// Lädt das Paket, ersetzt die Zieldateien über die <c>.old</c>-Rückfallebene und prüft
        /// danach mit einem echten Aufruf, ob das Werkzeug noch brauchbar antwortet. Fällt diese
        /// Kontrolle durch, wird der vorherige Stand zurückgeholt und
        /// <see cref="ToolInstallStatus.RolledBack"/> gemeldet. Wirft nicht bei einem
        /// Fehlschlag — der Ausgang steht im Rückgabewert; nur ein Abbruch über
        /// <paramref name="ct"/> kommt als <see cref="OperationCanceledException"/> heraus, wenn
        /// er nicht als <see cref="ToolInstallStatus.Canceled"/> abgefangen wurde.
        /// </summary>
        /// <param name="release">Ergebnis der Versionsprüfung, wenn eine vorliegt. <c>null</c> ist
        /// erlaubt: Dann nimmt das Werkzeug seine feste Rückfalladresse — genau der Fall
        /// „Erststart ohne Netzantwort der Metadatenquelle".</param>
        /// <param name="progress">Fortschritt des Downloads als Anteil (0.0–1.0).</param>
        /// <param name="stage">Meldet den Abschnittswechsel für die Statuszeile.</param>
        Task<ToolInstallOutcome> InstallAsync(
            ReleaseInfo? release,
            IProgress<double>? progress,
            IProgress<ToolInstallStage>? stage,
            CancellationToken ct);

        /// <summary>Entfernt alle <see cref="TargetPaths"/>. Wirft nicht; was nicht gelöscht
        /// werden konnte, steht im Ergebnis.</summary>
        ToolRemovalResult Uninstall();
    }
}
