using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services.Tools;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft die Beurteilung einer Versionsressource — reine Logik, ohne Datei- und Prozesszugriff.
///
/// <para>Alle Eingaben sind <b>echte, am 2026-08-31 auf einem Windows-11-Rechner ausgelesene
/// Werte</b>, keine erfundenen. Das ist hier wichtiger als sonst: Der ganze Sinn dieses Wegs ist,
/// dass er dieselbe Auskunft liefert wie der Programmstart — und das lässt sich nur gegen die
/// tatsächlichen Felder prüfen, nicht gegen angenommene.</para>
/// </summary>
public class ToolVersionResourceTests : IDisposable
{
    // yt-dlp.exe 2026.08.19, wie ausgeliefert.
    private static readonly VersionResourceInfo YtDlp = new(
        ProductName: "yt-dlp",
        FileDescription: "yt-dlp",
        CompanyName: "https://github.com/yt-dlp",
        FileVersion: "2026.08.19",
        ProductVersion: "2026.08.19 on Python 3.10.11");

    private static readonly VersionResourceInfo Git = new(
        ProductName: "Git",
        FileDescription: "Git for Windows",
        CompanyName: "The Git Development Community",
        FileVersion: "2.55.0.windows.3",
        ProductVersion: "2.55.0.windows.3");

    private static readonly VersionResourceInfo Curl = new(
        ProductName: "The curl executable",
        FileDescription: "The curl executable",
        CompanyName: "curl, https://curl.se/",
        FileVersion: "8.21.0",
        ProductVersion: "8.21.0");

    private static readonly VersionResourceInfo Notepad = new(
        ProductName: "Betriebssystem Microsoft® Windows®",
        FileDescription: "Editor",
        CompanyName: "Microsoft Corporation",
        FileVersion: "10.0.26100.8875 (WinBuild.160101.0800)",
        ProductVersion: "10.0.26100.8875");

    // ffmpeg.exe und ffprobe.exe der gyan.dev-Builds: gar keine Versionsressource.
    private static readonly VersionResourceInfo Leer = new(null, null, null, null, null);

    private static readonly string[] YtDlpNames = ["yt-dlp"];

    private readonly string _tempDir;
    private readonly string _tempLogDir;

    public ToolVersionResourceTests()
    {
        string root = Path.Combine(
            Path.GetTempPath(), "MortysDLP.Tests.ToolVersionResource", Guid.NewGuid().ToString("N"));
        _tempDir = Path.Combine(root, "files");
        _tempLogDir = Path.Combine(root, "logs");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_tempLogDir);
        Log.LogsDirectory = _tempLogDir;
    }

    public void Dispose()
    {
        Log.CloseForTests();
        try { Directory.Delete(Path.GetDirectoryName(_tempDir)!, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private static ToolProbe? Judge(VersionResourceInfo resource) =>
        ToolVersionResource.Judge(resource, YtDlpNames, YtDlpTool.IsYtDlpVersion);

    [Fact]
    public void EchteYtDlpRessource_LiefertVersionUndGiltAlsBrauchbar()
    {
        var probe = Judge(YtDlp);

        Assert.NotNull(probe);
        Assert.Equal(ToolHealth.Ok, probe.Health);
        Assert.True(probe.Usable);
        Assert.Equal("2026.08.19", probe.Version.Raw);
    }

    /// <summary>
    /// Der Kern: Ein fremdes Programm wird abgelehnt, <b>ohne es zu starten</b>. Genau das war
    /// vorher nicht möglich — die Ablehnung setzte voraus, dass das unbekannte Programm einmal
    /// ausgeführt wird.
    /// </summary>
    [Fact]
    public void FremdeProgramme_WerdenAbgelehntOhneStart()
    {
        foreach (var resource in new[] { Git, Curl, Notepad })
        {
            var probe = Judge(resource);

            Assert.NotNull(probe);
            Assert.Equal(ToolHealth.Foreign, probe.Health);
            Assert.False(probe.Usable);
        }
    }

    [Fact]
    public void FremdesProgramm_StehtLesbarImErgebnisFuerDasProtokoll()
    {
        var probe = Judge(Git);

        Assert.NotNull(probe);
        Assert.Contains("Git", probe.Answer!, StringComparison.Ordinal);
        Assert.Contains("2.55.0.windows.3", probe.Answer!, StringComparison.Ordinal);
    }

    /// <summary>Keine Versionsressource ist <b>keine</b> Ablehnung — das wäre der Fehler, der
    /// ffmpeg unbrauchbar machen würde. Der Aufrufer muss in diesem Fall fragen.</summary>
    [Fact]
    public void LeereRessource_FuehrtZumFragenStattZurAblehnung()
    {
        Assert.True(Leer.IsEmpty);
        Assert.Null(Judge(Leer));
    }

    /// <summary>Der Name passt, die Version nicht: Das ist kein fremdes Programm, sondern eine
    /// Ressource, aus der sich die Version nicht ablesen lässt — etwa eine selbst gebaute Fassung
    /// mit stehen gebliebenem <c>0.0.0.0</c>. Auch hier muss gefragt werden, nicht abgelehnt.</summary>
    [Theory]
    [InlineData("0.0.0.0", null)]
    [InlineData("", "")]
    [InlineData("nightly", "nightly")]
    public void NamePasstAberVersionNicht_FuehrtZumFragen(string? fileVersion, string? productVersion)
    {
        var probe = Judge(YtDlp with { FileVersion = fileVersion, ProductVersion = productVersion });

        Assert.Null(probe);
    }

    /// <summary>Steht die brauchbare Angabe nur in <c>ProductVersion</c> (mit Zusatz dahinter),
    /// wird sie von dort gelesen. Bei yt-dlp lautet sie
    /// <c>2026.08.19 on Python 3.10.11</c> — das erste Wort ist die Version.</summary>
    [Fact]
    public void VersionAusProductVersion_WirdAlsRueckfallGelesen()
    {
        var probe = Judge(YtDlp with { FileVersion = "0.0.0.0" });

        Assert.NotNull(probe);
        Assert.Equal(ToolHealth.Ok, probe.Health);
        Assert.Equal("2026.08.19", probe.Version.Raw);
    }

    [Fact]
    public void NamensvergleichIgnoriertGrossschreibungUndLeerraum()
    {
        var probe = Judge(YtDlp with { ProductName = "  YT-DLP  ", FileDescription = null });

        Assert.NotNull(probe);
        Assert.Equal(ToolHealth.Ok, probe.Health);
    }

    /// <summary>Der Identitätsnachweis läuft über <b>dieselbe</b> Regel wie beim Prozessaufruf.
    /// Eine Ressource, die sich yt-dlp nennt, aber keine Datumsversion trägt, kommt deshalb auch
    /// hier nicht als gültige Version durch.</summary>
    [Fact]
    public void VersionsschemaGiltAuchHier_KeineDatumsversionKeinTreffer()
    {
        var probe = Judge(YtDlp with { FileVersion = "2.47.1", ProductVersion = "2.47.1" });

        Assert.Null(probe);
    }

    [Fact]
    public void FehlendeDatei_LiefertNull()
    {
        Assert.Null(ToolVersionResource.TryRead(Path.Combine(_tempDir, "gibtesnicht.exe")));
    }

    /// <summary>Eine 0-Byte-Datei wird wie „nicht vorhanden" behandelt — sonst würde eine Hülle
    /// aus einem abgebrochenen Download hier als lesbare Datei durchgehen.</summary>
    [Fact]
    public void LeereDatei_LiefertNull()
    {
        string path = Path.Combine(_tempDir, "leer.exe");
        File.WriteAllBytes(path, []);

        Assert.Null(ToolVersionResource.TryRead(path));
    }

    /// <summary>Eine Datei ohne Versionsressource (hier: schlichter Text) liefert <c>null</c> und
    /// nicht etwa leere Felder — der Aufrufer soll „nichts erfahren" nicht mit „nichts drin"
    /// verwechseln müssen.</summary>
    [Fact]
    public void DateiOhneVersionsressource_LiefertNull()
    {
        string path = Path.Combine(_tempDir, "kein-pe.exe");
        File.WriteAllText(path, "das ist keine ausfuehrbare Datei");

        Assert.Null(ToolVersionResource.TryRead(path));
    }

    /// <summary>
    /// Die Verdrahtung: Liefert der prozessfreie Weg eine Antwort, darf <c>ProbeAsync</c> das
    /// Programm <b>nicht</b> mehr starten. Ohne diesen Test wäre der ganze Gewinn von einer
    /// vergessenen Zeile abhängig, die im Betrieb nur als „ist wieder langsam" auffällt.
    /// </summary>
    [Fact]
    public async Task ProbeAsync_NimmtDenProzessfreienWegUndStartetNichts()
    {
        var tool = new FakeTool(new ToolProbe(ToolHealth.Ok, ToolVersion.Parse("2026.08.19"), "aus der Datei"));

        var probe = await tool.ProbeAsync(CancellationToken.None);

        Assert.Equal(ToolHealth.Ok, probe.Health);
        Assert.Equal("2026.08.19", probe.Version.Raw);
        Assert.False(tool.ProcessWasStarted);
    }

    /// <summary>Und umgekehrt: Ohne Antwort aus der Datei muss gefragt werden. Ein Werkzeug wie
    /// ffmpeg, dessen Dateien keine Versionsressource tragen, darf durch diese Abkürzung nicht
    /// unbrauchbar werden.</summary>
    [Fact]
    public async Task ProbeAsync_OhneAntwortAusDerDatei_FragtDasProgramm()
    {
        var tool = new FakeTool(withoutProcess: null);

        var probe = await tool.ProbeAsync(CancellationToken.None);

        // Nur der Prozessweg kann "nicht installiert" melden - der prozessfreie Weg liefert in
        // diesem Fall null und fällt durch. Das Ergebnis ist damit der Beleg dafür, welcher Weg
        // beschritten wurde, ohne dass der Test dafür ein echtes Programm braucht.
        Assert.Equal(ToolHealth.NotInstalled, probe.Health);
    }

    /// <summary>Kleinstmögliches Werkzeug — zeigt gleichzeitig, was die Abstraktion von einem
    /// neuen Werkzeug verlangt.</summary>
    private sealed class FakeTool(ToolProbe? withoutProcess) : ManagedToolBase
    {
        public bool ProcessWasStarted { get; private set; }

        public override string Id => "fake";

        public override string DisplayName => "Fake-Werkzeug";

        public override bool RequiredForOperation => false;

        public override ToolUpdatePolicy UpdatePolicy => ToolUpdatePolicy.OnlyWhenNewer;

        public override IReadOnlyList<string> TargetPaths => [VersionExecutable];

        public override IReadOnlyList<MortysDLP.Services.Releases.IReleaseSource> CreateSources() => [];

        public override MortysDLP.Services.Releases.ReleaseQuery CreateQuery() => new("owner", "repo");

        public override Task<ToolInstallOutcome> InstallAsync(
            MortysDLP.Services.Releases.ReleaseInfo? release,
            IProgress<double>? progress,
            IProgress<ToolInstallStage>? stage,
            CancellationToken ct) =>
            throw new NotSupportedException("wird in diesem Test nicht gebraucht");

        protected override string VersionExecutable =>
            Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.FakeTool", "gibtesnicht.exe");

        protected override IReadOnlyList<string> VersionArguments => ["--version"];

        protected override string? ExtractVersion(string output)
        {
            ProcessWasStarted = true;
            return null;
        }

        protected override bool IsOwnVersion(ToolVersion version) => version.IsOrdering;

        protected override ToolProbe? TryProbeWithoutProcess() => withoutProcess;
    }
}
