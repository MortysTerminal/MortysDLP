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

    /// <summary>Bewusst ohne <c>github-atom</c>: Der Atom-Feed kennt keine
    /// Vorabversions-Kennzeichnung und griff im echten Testlauf einen assetlosen <c>vX.Y.Z</c>-Tag,
    /// was den Download mit 404 scheitern ließ, statt auf die versionslose Rückfalladresse
    /// auszuweichen (siehe <see cref="ReleaseSources.CreateWhisperChain"/>).</summary>
    [Fact]
    public void Quellenkette_FragtNurDasEchteLatestOhneAtom()
    {
        var names = _tool.CreateSources().Select(s => s.Name).ToList();

        Assert.Equal(["github-api-latest", "github-redirect"], names);
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
}
