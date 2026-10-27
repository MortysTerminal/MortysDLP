using MortysDLP.Models;
using MortysDLP.Services.Releases;
using MortysDLP.Services.Tools;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft whisper.cpp: Aufbau des Werkzeugs, den Identitätsnachweis über die echte
/// <c>--version</c>-Ausgabe (am 2026-09-01 gegen die frisch heruntergeladene Datei abgeschrieben,
/// nicht erfunden) und die gezielte Auswahl beim Entpacken. Installation und Versionsabruf
/// brauchen ein echtes Werkzeug und eine echte Verbindung und gehören deshalb in den
/// Handtestplan, nicht hierher.
/// </summary>
public class WhisperToolTests : IDisposable
{
    /// <summary>Die tatsächliche Ausgabe von <c>whisper-cli.exe --version</c> auf
    /// <c>stdout</c> — die Diagnosezeilen zum geladenen Backend stehen auf <c>stderr</c> und
    /// erreichen <c>ExtractVersion</c> deshalb nie.</summary>
    private const string RealWhisperOutput = "whisper.cpp version: 1.9.3";

    private readonly WhisperTool _tool = new();
    private readonly string _tempDir;

    public WhisperToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.WhisperTool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private static AppVersion V(string text) =>
        AppVersion.TryParse(text, out var version) ? version : throw new ArgumentException(text);

    private static ToolProbe Installed(string version) =>
        new(ToolHealth.Ok, ToolVersion.Parse(version), $"whisper.cpp version: {version}");

    [Fact]
    public void IstOptionalUndVergleichtNurAufUngleichheit()
    {
        Assert.Equal("whisper", _tool.Id);
        Assert.False(_tool.RequiredForOperation);
        Assert.Equal(ToolUpdatePolicy.WhenDifferent, _tool.UpdatePolicy);
    }

    [Fact]
    public void HatGenauEineZieldatei()
    {
        Assert.Single(_tool.TargetPaths);
        Assert.EndsWith(Path.Combine("Whisper", "whisper.exe"), _tool.TargetPaths[0],
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Nur die Commit-Zuordnung: <c>/releases/latest</c> zeigt seit dem Schemawechsel auf
    /// ein stabiles Versions-Release ohne Anhänge - eine Quelle, die das abfragt, beendet die Kette
    /// mit „Version ohne Paket" und verhindert den Rückfall (siehe
    /// <see cref="ReleaseSources.CreateWhisperChain"/>).</summary>
    [Fact]
    public void Quellenkette_FragtNurDieCommitZuordnung()
    {
        var names = _tool.CreateSources().Select(s => s.Name).ToList();

        Assert.Equal(["github-api-commit-paired"], names);
    }

    /// <summary>Die feste Rückfalladresse ist gegen das echte Release <c>b5454</c> geprüft
    /// (2026-10-26): Größe und SHA-256 stammen aus der GitHub-API und stimmen mit der
    /// heruntergeladenen Datei überein.</summary>
    [Fact]
    public void FesteRueckfalladresse_IstGepruftesPaketMitPruefsumme()
    {
        var candidates = _tool.ResolveDownloadCandidates(null, ToolProbe.NotInstalled);

        var only = Assert.Single(candidates);
        Assert.Equal("https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-blas-bin-x64.zip", only.Url);
        Assert.Equal(21715607, only.Size);
        Assert.Equal("b1158bd10edf92c9799149a6fc5d3e58da3f495241d6fb3423088130e88ffbf3", only.Sha256);
        Assert.True(MortysDLP.Helpers.UrlSafety.IsAllowed(new Uri(only.Url)));
    }

    [Fact]
    public void ReleaseMitAdresse_WirdZuerstVersuchtDanachDieFesteAdresse()
    {
        var release = new ReleaseInfo(V("1.9.6"), "https://github.com/ggml-org/whisper.cpp/releases/download/b5500/whisper-blas-bin-x64.zip",
            null, 22000000, "cd34", "test", []);

        var candidates = _tool.ResolveDownloadCandidates(release, ToolProbe.NotInstalled);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(release.DownloadUrl, candidates[0].Url);
        Assert.Equal("cd34", candidates[0].Sha256);
        Assert.Contains("/b5454/", candidates[1].Url);
    }

    /// <summary>Genau der Fall, der die Installation gebrochen hat: eine Version ohne Anhang und
    /// ohne Adresse. Es darf keine geratene <c>releases/latest/download/…</c>-Adresse entstehen.</summary>
    [Fact]
    public void ReleaseOhneAnhangUndAdresse_NutztNurDieFesteAdresse()
    {
        var release = new ReleaseInfo(V("1.9.5"), null, null, null, null, "test", []);

        var candidates = _tool.ResolveDownloadCandidates(release, ToolProbe.NotInstalled);

        var only = Assert.Single(candidates);
        Assert.Contains("/b5454/", only.Url);
        Assert.DoesNotContain(candidates, c => c.Url.Contains("/latest/", StringComparison.Ordinal));
    }

    /// <summary>Die feste Adresse darf eine neuere Installation nie ersetzen — sonst stufte ein
    /// einziger 404 auf der Release-Adresse 1.9.6 still auf 1.9.5 zurück.</summary>
    [Fact]
    public void NeuereInstallation_FesteAdresseIstKeinRueckfall()
    {
        var release = new ReleaseInfo(V("1.9.7"), "https://github.com/ggml-org/whisper.cpp/releases/download/b5600/whisper-blas-bin-x64.zip",
            null, 22000000, "cd34", "test", []);

        var candidates = _tool.ResolveDownloadCandidates(release, Installed("1.9.6"));

        var only = Assert.Single(candidates);
        Assert.Equal(release.DownloadUrl, only.Url);
    }

    [Fact]
    public void GleicheVersionInstalliert_OhneReleaseAdresse_KeinKandidat()
    {
        var candidates = _tool.ResolveDownloadCandidates(null, Installed(WhisperTool.PinnedVersion));

        Assert.Empty(candidates);
    }

    [Theory]
    [InlineData("1.9.3")]
    [InlineData("1.8.7")]
    public void AeltereInstallation_FesteAdresseBleibtRueckfall(string installed)
    {
        var candidates = _tool.ResolveDownloadCandidates(null, Installed(installed));

        Assert.Contains("/b5454/", Assert.Single(candidates).Url);
    }

    /// <summary>Reparieren: Die Datei ist da, antwortet aber nicht — dann zählt sie nicht als
    /// „neuer" und die feste Adresse muss greifen.</summary>
    [Fact]
    public void InstallationAntwortetNicht_FesteAdresseBleibtRueckfall()
    {
        var broken = new ToolProbe(ToolHealth.NoAnswer, ToolVersion.Parse("1.9.6"), null);

        var candidates = _tool.ResolveDownloadCandidates(null, broken);

        Assert.Contains("/b5454/", Assert.Single(candidates).Url);
    }

    [Fact]
    public void ReleaseIstDieFesteAdresse_WirdNurEinmalVersucht()
    {
        var release = new ReleaseInfo(V("1.9.5"),
            "https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-blas-bin-x64.zip",
            null, 21715607, "b1158bd10edf92c9799149a6fc5d3e58da3f495241d6fb3423088130e88ffbf3", "test", []);

        Assert.Single(_tool.ResolveDownloadCandidates(release, ToolProbe.NotInstalled));
    }

    [Fact]
    public void Anfrage_NenntAktuellesOwnerRepoUndAdressvorlage()
    {
        var query = _tool.CreateQuery();

        // ggml-org, nicht ggerganov - der alte Name ist nur noch eine Weiterleitung, die die
        // Redirect-Quelle nicht mehr auf den Tag führt.
        Assert.Equal("ggml-org", query.Owner);
        Assert.Equal("whisper.cpp", query.Repo);
        Assert.Equal("whisper-blas-bin-x64.zip", query.AssetPattern);
        Assert.NotNull(query.DownloadUrlTemplate);
    }

    [Fact]
    public void Adressvorlage_LoestSichZuEinerErlaubtenAdresseAuf()
    {
        string? url = _tool.CreateQuery().ResolveDownloadUrl("v1.9.3");

        Assert.Equal(
            "https://github.com/ggml-org/whisper.cpp/releases/download/v1.9.3/whisper-blas-bin-x64.zip",
            url);
        Assert.True(MortysDLP.Helpers.UrlSafety.IsAllowed(new Uri(url!)));
    }

    [Fact]
    public void EchteAusgabe_WirdAlsVersionGelesen()
    {
        string? version = WhisperTool.ExtractVersionToken(RealWhisperOutput);

        Assert.Equal("1.9.3", version);
        Assert.True(ToolVersion.Parse(version).HasNumericCore);
    }

    /// <summary>Der Identitätsnachweis: Die Zeile muss mit <c>whisper.cpp version:</c>
    /// beginnen. Ein bloßes Vorkommen der Zahl irgendwo in einer fremden Zeile genügt nicht.</summary>
    [Theory]
    [InlineData("git version 2.55.0.windows.3")]
    [InlineData("ffmpeg version 9.0.1-essentials_build-www.gyan.dev")]
    [InlineData("1.9.3")]
    [InlineData("Version: whisper.cpp 1.9.3")]
    [InlineData("")]
    [InlineData("   ")]
    public void FremdeAusgabe_WirdNichtAlsVersionGelesen(string output)
    {
        Assert.Null(WhisperTool.ExtractVersionToken(output));
    }

    /// <summary>
    /// Das echte Paket bringt neben whisper.cpp selbst ein knappes Dutzend fremder
    /// Beispielprogramme und -bibliotheken mit (am 2026-09-01 gegen das echte Release geprüft:
    /// u. a. <c>llama.dll</c>, <c>parakeet.exe</c>, <c>SDL2.dll</c>). Diese dürfen nicht mit
    /// ausgepackt werden - nur was <c>whisper-cli.exe</c> zum Laufen braucht.
    /// </summary>
    [Fact]
    public void Entpacken_HoltNurDasHauptprogrammUndSeineLaufzeitbibliotheken()
    {
        string zip = CreateZip(
            ("whisper-cli.exe", "whisper-inhalt"),
            ("ggml.dll", "ggml-inhalt"),
            ("ggml-cpu-x64.dll", "ggml-cpu-inhalt"),
            ("libopenblas.dll", "openblas-inhalt"),
            ("llama.dll", "fremd"),
            ("parakeet-cli.exe", "fremd"),
            ("SDL2.dll", "fremd"),
            ("bin/README.txt", "belanglos"));

        string stagedExe = Path.Combine(_tempDir, "whisper.exe.new");
        string whisperDir = _tempDir;

        bool found = WhisperTool.ExtractPackage(zip, whisperDir, stagedExe);

        Assert.True(found);
        Assert.Equal("whisper-inhalt", File.ReadAllText(stagedExe));
        Assert.Equal("ggml-inhalt", File.ReadAllText(Path.Combine(whisperDir, "ggml.dll")));
        Assert.Equal("ggml-cpu-inhalt", File.ReadAllText(Path.Combine(whisperDir, "ggml-cpu-x64.dll")));
        Assert.Equal("openblas-inhalt", File.ReadAllText(Path.Combine(whisperDir, "libopenblas.dll")));

        Assert.False(File.Exists(Path.Combine(whisperDir, "llama.dll")));
        Assert.False(File.Exists(Path.Combine(whisperDir, "parakeet-cli.exe")));
        Assert.False(File.Exists(Path.Combine(whisperDir, "SDL2.dll")));
        Assert.False(File.Exists(Path.Combine(whisperDir, "README.txt")));
    }

    /// <summary>Ältere whisper.cpp-Ausgaben nannten das Hauptprogramm <c>main.exe</c> - der
    /// Vorgängercode hat damit gearbeitet, deshalb bleibt der Name als zweiter Kandidat gültig.</summary>
    [Fact]
    public void Entpacken_ErkenntAuchDenAelterenNamenMainExe()
    {
        string zip = CreateZip(("main.exe", "whisper-inhalt"), ("ggml.dll", "ggml-inhalt"));
        string stagedExe = Path.Combine(_tempDir, "whisper.exe.new");

        bool found = WhisperTool.ExtractPackage(zip, _tempDir, stagedExe);

        Assert.True(found);
        Assert.Equal("whisper-inhalt", File.ReadAllText(stagedExe));
    }

    [Fact]
    public void Entpacken_OhneHauptprogramm_MeldetNichtGefunden()
    {
        string zip = CreateZip(("ggml.dll", "ggml-inhalt"), ("llama.dll", "fremd"));
        string stagedExe = Path.Combine(_tempDir, "whisper.exe.new");

        bool found = WhisperTool.ExtractPackage(zip, _tempDir, stagedExe);

        Assert.False(found);
        Assert.False(File.Exists(stagedExe));
    }

    private string CreateZip(params (string EntryName, string Content)[] entries)
    {
        string path = Path.Combine(_tempDir, $"paket-{Guid.NewGuid():N}.zip");

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (var (entryName, content) in entries)
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        return path;
    }

    // --- Deinstallation: das ganze Paket, nicht nur die EXE -----------------------------

    /// <summary>Legt ein Abbild einer echten Installation an: Programm, Laufzeitbibliotheken,
    /// Reste früherer Aktualisierungen und ein Modell im Unterordner.</summary>
    private (string WhisperDir, string ModelsDir) CreateInstallation()
    {
        string whisperDir = Path.Combine(_tempDir, "Whisper");
        string modelsDir = Path.Combine(whisperDir, "models");
        Directory.CreateDirectory(modelsDir);
        foreach (string name in new[] { "whisper.exe", "whisper.dll", "ggml.dll", "ggml-base.dll", "ggml-cpu-haswell.dll", "libopenblas.dll", "whisper.exe.old" })
            File.WriteAllText(Path.Combine(whisperDir, name), name);
        File.WriteAllText(Path.Combine(modelsDir, "ggml-base.bin"), "modell");
        return (whisperDir, modelsDir);
    }

    /// <summary>Genau der gemeldete Fehler: Nach dem Deinstallieren blieben der Ordner und die
    /// DLLs liegen, weil nur die EXE entfernt wurde.</summary>
    [Fact]
    public void RemovePackage_Vollstaendig_EntferntOrdnerSamtDllsUndModellen()
    {
        var (whisperDir, modelsDir) = CreateInstallation();

        var result = WhisperTool.RemovePackage(whisperDir, modelsDir, keepModels: false);

        Assert.True(result.Success);
        Assert.False(Directory.Exists(whisperDir));
    }

    [Fact]
    public void RemovePackage_ModelleBehalten_EntferntAllesAusserDemModellordner()
    {
        var (whisperDir, modelsDir) = CreateInstallation();
        Directory.CreateDirectory(Path.Combine(whisperDir, "fremd"));

        var result = WhisperTool.RemovePackage(whisperDir, modelsDir, keepModels: true);

        Assert.True(result.Success);
        Assert.Equal(["models"], Directory.GetFileSystemEntries(whisperDir).Select(Path.GetFileName));
        Assert.True(File.Exists(Path.Combine(modelsDir, "ggml-base.bin")));
    }

    [Fact]
    public void RemovePackage_ModelleBehaltenOhneModelle_EntferntAuchDenLeerenOrdner()
    {
        string whisperDir = Path.Combine(_tempDir, "Whisper");
        Directory.CreateDirectory(whisperDir);
        File.WriteAllText(Path.Combine(whisperDir, "whisper.exe"), "x");

        var result = WhisperTool.RemovePackage(whisperDir, Path.Combine(whisperDir, "models"), keepModels: true);

        Assert.True(result.Success);
        Assert.False(Directory.Exists(whisperDir));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RemovePackage_NichtsInstalliert_WirftNichtUndMeldetNichts(bool keepModels)
    {
        string whisperDir = Path.Combine(_tempDir, "gibt-es-nicht");

        var result = WhisperTool.RemovePackage(whisperDir, Path.Combine(whisperDir, "models"), keepModels);

        Assert.True(result.Success);
        Assert.Empty(result.RemovedPaths);
    }

    [Fact]
    public void RemovePackage_MeldetEntfernteEintraege()
    {
        var (whisperDir, modelsDir) = CreateInstallation();

        var result = WhisperTool.RemovePackage(whisperDir, modelsDir, keepModels: true);

        Assert.Contains(result.RemovedPaths, p => p.EndsWith("libopenblas.dll", StringComparison.Ordinal));
        Assert.DoesNotContain(result.RemovedPaths, p => p.Contains("models", StringComparison.Ordinal));
    }

    /// <summary>Nur whisper.cpp bietet „Modelle behalten" an — für alle anderen bleibt die
    /// einfache Ja/Nein-Rückfrage.</summary>
    [Fact]
    public void NurWhisperHatNutzerdaten()
    {
        var withData = ToolCatalog.CreateAll().Where(t => t.HasUserData).Select(t => t.Id);

        Assert.Equal(["whisper"], withData);
    }

    [Fact]
    public void Whisper_HatNutzerdaten()
    {
        Assert.True(_tool.HasUserData);
    }
}
