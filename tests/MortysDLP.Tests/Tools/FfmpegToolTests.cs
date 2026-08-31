using MortysDLP.Models;
using MortysDLP.Services.Tools;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft den unbequemen Fall der Abstraktion: zwei Zieldateien aus einem ZIP, eine nicht ordnende
/// Version, eine Versionsquelle, die nur Text liefert.
/// </summary>
public class FfmpegToolTests : IDisposable
{
    /// <summary>Die tatsächliche erste Zeile von <c>ffmpeg -version</c> einer
    /// gyan.dev-„essentials"-Ausgabe — abgeschrieben, nicht erfunden.</summary>
    private const string RealFfmpegOutput =
        "ffmpeg version 7.1-essentials_build-www.gyan.dev Copyright (c) 2000-2024 the FFmpeg developers\n" +
        "built with gcc 14.2.0 (Rev1, Built by MSYS2 project)\n";

    private const string RealFfprobeOutput =
        "ffprobe version 7.1-essentials_build-www.gyan.dev Copyright (c) 2007-2024 the FFmpeg developers\n";

    private readonly FfmpegTool _tool = new();
    private readonly string _tempDir;

    public FfmpegToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.FfmpegTool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void IstErforderlichUndVergleichtNurAufUngleichheit()
    {
        Assert.Equal("ffmpeg", _tool.Id);
        Assert.True(_tool.RequiredForOperation);
        Assert.Equal(ToolUpdatePolicy.WhenDifferent, _tool.UpdatePolicy);
    }

    /// <summary>ffmpeg und ffprobe sind ein Werkzeug, nicht zwei: Sie kommen aus demselben Paket
    /// und werden gemeinsam ersetzt oder gemeinsam zurückgeholt.</summary>
    [Fact]
    public void HatZweiZieldateien()
    {
        Assert.Equal(2, _tool.TargetPaths.Count);
        Assert.Contains(_tool.TargetPaths, p => p.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_tool.TargetPaths, p => p.EndsWith("ffprobe.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Quellenkette_IstEinAbschliessenderTextendpunkt()
    {
        var sources = _tool.CreateSources();

        Assert.Single(sources);
        Assert.Equal("gyan-dev-release-version", sources[0].Name);
        Assert.True(sources[0].IsAuthoritative);
    }

    /// <summary>Die Anfrage trägt die Text-URL und bewusst <b>keine</b> Adressvorlage: Die
    /// Paketadresse ist fest und enthält keine Version, sie lässt sich also nicht aus einem Tag
    /// zusammensetzen.</summary>
    [Fact]
    public void Anfrage_NenntTextEndpunktUndKeineAdressvorlage()
    {
        var query = _tool.CreateQuery();

        Assert.Equal("https://www.gyan.dev/ffmpeg/builds/release-version", query.PlainTextVersionUrl);
        Assert.Null(query.DownloadUrlTemplate);
        Assert.Null(query.PackageName);
        Assert.True(MortysDLP.Helpers.UrlSafety.IsAllowed(new Uri(query.PlainTextVersionUrl!)));
    }

    [Theory]
    [InlineData(RealFfmpegOutput, "ffmpeg", "7.1-essentials_build-www.gyan.dev")]
    [InlineData(RealFfprobeOutput, "ffprobe", "7.1-essentials_build-www.gyan.dev")]
    [InlineData("ffmpeg version 6.0 Copyright (c) 2000-2023", "ffmpeg", "6.0")]
    [InlineData("ffmpeg version n7.1-11-g123abc Copyright", "ffmpeg", "n7.1-11-g123abc")]
    public void Versionszeile_WirdAusDerEchtenAusgabeGelesen(string output, string program, string expected)
    {
        Assert.Equal(expected, FfmpegTool.ExtractVersionToken(output, program));
    }

    [Theory]
    [InlineData("", "ffmpeg")]
    [InlineData("ffmpeg ohne das Wort dahinter", "ffmpeg")]
    [InlineData("version ", "ffmpeg")]
    public void UnbrauchbareAusgabe_LiefertKeineVersion(string output, string program)
    {
        Assert.Null(FfmpegTool.ExtractVersionToken(output, program));
    }

    /// <summary>
    /// Der Identitätsnachweis: Die Zeile muss mit <c>&lt;programm&gt; version</c> <b>beginnen</b>.
    /// Die beiden letzten Fälle sind der Grund dafür — beide Werkzeuge der ffmpeg-Familie
    /// schließen dieselbe Zeile mit <c>the FFmpeg developers</c> ab. Ein Enthalten-Test auf
    /// „ffmpeg" hätte die ffprobe-Ausgabe deshalb als ffmpeg-Ausgabe akzeptiert; genau das ist
    /// beim Schreiben dieses Tests aufgefallen.
    /// </summary>
    [Theory]
    [InlineData("git version 2.47.1.windows.1", "ffmpeg")]
    [InlineData("curl 8.9.1 (x86_64) libcurl/8.9.1 version blah", "ffmpeg")]
    [InlineData("Das hier ist die ffmpeg version 1.0 von irgendwem", "ffmpeg")]
    [InlineData(RealFfprobeOutput, "ffmpeg")]
    [InlineData(RealFfmpegOutput, "ffprobe")]
    public void FremdeAusgabe_WirdAbgelehnt(string output, string program)
    {
        Assert.Null(FfmpegTool.ExtractVersionToken(output, program));
    }

    /// <summary>Eine vorangestellte Warnzeile darf ein echtes ffmpeg nicht zum fremden Programm
    /// machen — die Versionszeile wird in den ersten Zeilen gesucht, nicht nur in der ersten.</summary>
    [Fact]
    public void VorangestellteWarnzeile_VerhindertDieErkennungNicht()
    {
        string output = "WARNING: irgendeine Meldung der Laufzeitumgebung\n" + RealFfmpegOutput;

        Assert.Equal("7.1-essentials_build-www.gyan.dev",
            FfmpegTool.ExtractVersionToken(output, "ffmpeg"));
    }

    /// <summary>Der Weg von der echten Werkzeugausgabe bis zur Entscheidung, in einem Test:
    /// Was <c>ffmpeg -version</c> ausgibt, darf gegen die Nummer des Versionsendpunkts kein
    /// Update auslösen.</summary>
    [Fact]
    public void EchteAusgabeGegenVersionsendpunkt_LoestKeinUpdateAus()
    {
        var local = ToolVersion.Parse(FfmpegTool.ExtractVersionToken(RealFfmpegOutput, "ffmpeg"));
        var remote = ToolVersion.Parse("7.1");

        var verdict = ToolUpdateDecision.Evaluate(local, remote, _tool.UpdatePolicy);

        Assert.False(verdict.Offer);
    }

    [Fact]
    public void Entpacken_HoltBeideDateienAusDemVersionsbenanntenUnterordner()
    {
        string zip = CreateZip(
            ("ffmpeg-7.1-essentials_build/bin/ffmpeg.exe", "ffmpeg-inhalt"),
            ("ffmpeg-7.1-essentials_build/bin/ffprobe.exe", "ffprobe-inhalt"),
            ("ffmpeg-7.1-essentials_build/README.txt", "belanglos"));

        string ffmpegTarget = Path.Combine(_tempDir, "ffmpeg.exe.new");
        string ffprobeTarget = Path.Combine(_tempDir, "ffprobe.exe.new");

        var missing = FfmpegTool.ExtractExecutables(zip,
            [("ffmpeg.exe", ffmpegTarget), ("ffprobe.exe", ffprobeTarget)]);

        Assert.Empty(missing);
        Assert.Equal("ffmpeg-inhalt", File.ReadAllText(ffmpegTarget));
        Assert.Equal("ffprobe-inhalt", File.ReadAllText(ffprobeTarget));
    }

    [Fact]
    public void Entpacken_MeldetFehlendeEintraegeStattStillZuSchweigen()
    {
        string zip = CreateZip(("build/bin/ffmpeg.exe", "ffmpeg-inhalt"));

        var missing = FfmpegTool.ExtractExecutables(zip,
            [
                ("ffmpeg.exe", Path.Combine(_tempDir, "ffmpeg.exe.new")),
                ("ffprobe.exe", Path.Combine(_tempDir, "ffprobe.exe.new")),
            ]);

        Assert.Single(missing);
        Assert.Equal("ffprobe.exe", missing[0]);
    }

    /// <summary>Ein Eintrag, der auf einen Pfad außerhalb des Ziels zeigt, kann hier nichts
    /// ausrichten: Es wird nicht das Archiv in ein Verzeichnis entpackt, sondern es werden
    /// namentlich gesuchte Einträge in einen von MortysDLP bestimmten Pfad geschrieben. Der Test
    /// hält das fest, damit es beim Umbau nicht versehentlich zu einem
    /// <c>ExtractToDirectory</c> zurückgebaut wird.</summary>
    [Fact]
    public void Entpacken_EinAusbrechenderEintragsnameAendertDasZielNicht()
    {
        string zip = CreateZip(("../../../boese/ffmpeg.exe", "boese"));

        string target = Path.Combine(_tempDir, "ffmpeg.exe.new");
        var missing = FfmpegTool.ExtractExecutables(zip, [("ffmpeg.exe", target)]);

        Assert.Empty(missing);
        Assert.True(File.Exists(target));
        Assert.Equal("boese", File.ReadAllText(target));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "..", "..", "..", "boese")));
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
