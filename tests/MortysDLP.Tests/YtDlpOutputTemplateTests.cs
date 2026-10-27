using MortysDLP.Services;
using System.IO;
using System.Text;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft den Bau der yt-dlp-Ausgabevorlage: Maskierung wörtlicher Teile, Längenbudget gegen
/// MAX_PATH und das Lesen der von <c>--print-to-file</c> geschriebenen Pfaddatei.
/// </summary>
public class YtDlpOutputTemplateTests : IDisposable
{
    private const string Dir = @"C:\Users\x\Videos";

    private readonly string _tempDir;

    public YtDlpOutputTemplateTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.YtDlpOutputTemplate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>Gekürzt wird nach <b>Bytes</b> (UTF-8), nicht nach Zeichen: Ein Zeichen braucht in
    /// UTF-8 nie weniger Bytes als in UTF-16 Einheiten — so bleibt auch ein Titel aus Emoji (zwei
    /// UTF-16-Einheiten je Zeichen, mit denen Windows MAX_PATH zählt) sicher im Budget.</summary>
    [Fact]
    public void OhneEigenenNamen_TitelPlatzhalterMitHoechstens120Bytes()
    {
        string template = YtDlpOutputTemplate.Build(Dir, literalBaseName: null, literalSuffix: "_q1080_mp4");

        Assert.Equal(@"C:\Users\x\Videos\%(title).120B_q1080_mp4_%(id)s.%(ext)s", template);
    }

    [Fact]
    public void LangerZielordner_BuildVerwendetDasVerkleinerteBudget()
    {
        string longDir = @"C:\" + string.Join('\\', Enumerable.Repeat("Unterordner", 12));
        int budget = YtDlpOutputTemplate.TitleBudget(longDir, "_q1080_mp4");

        string template = YtDlpOutputTemplate.Build(longDir, null, "_q1080_mp4");

        Assert.True(budget < YtDlpOutputTemplate.MaxTitleLength);
        Assert.Contains($"%(title).{budget}B_q1080_mp4", template, StringComparison.Ordinal);
    }

    [Fact]
    public void LangerZielordner_EigenerNameWirdAufDasVerkleinerteBudgetGekuerzt()
    {
        string longDir = @"C:\" + string.Join('\\', Enumerable.Repeat("Unterordner", 12));
        int budget = YtDlpOutputTemplate.TitleBudget(longDir, "");

        Assert.Equal(budget, YtDlpOutputTemplate.LiteralBaseName(longDir, new string('a', 300), "").Length);
    }

    /// <summary>Was im Protokoll steht, muss der tatsächliche Name sein — <see cref="YtDlpOutputTemplate.Build"/>
    /// und die Anzeige verwenden dieselbe Funktion.</summary>
    [Fact]
    public void LiteralBaseName_EntsprichtDemNamenInDerVorlage()
    {
        string name = YtDlpOutputTemplate.LiteralBaseName(Dir, "Mein Video: Teil 1", "_x");

        Assert.Equal($@"C:\Users\x\Videos\{name}_x_%(id)s.%(ext)s", YtDlpOutputTemplate.Build(Dir, "Mein Video: Teil 1", "_x"));
    }

    /// <summary>yt-dlp setzt in der Vorlage Umgebungsvariablen ein (<c>os.path.expandvars</c>) —
    /// ein Titel „$USERNAME live" würde sonst zum Benutzernamen.</summary>
    [Fact]
    public void DollarImEigenenNamen_WirdErsetzt()
    {
        string name = YtDlpOutputTemplate.LiteralBaseName(Dir, "$USERNAME live", "");

        Assert.DoesNotContain('$', name);
        Assert.Equal("\uFF04USERNAME live", name);
    }

    [Theory]
    [InlineData("100%", "100%%")]
    [InlineData("%(id)s", "%%(id)s")]
    [InlineData("kein Prozent", "kein Prozent")]
    [InlineData("%%", "%%%%")]
    public void EscapeLiteral_VerdoppeltJedesProzentzeichen(string literal, string erwartet)
    {
        Assert.Equal(erwartet, YtDlpOutputTemplate.EscapeLiteral(literal));
    }

    /// <summary>Genau der Absturzfall: ein Twitch-Titel oder eigener Name mit <c>%</c>.</summary>
    [Fact]
    public void EigenerNameMitProzent_WirdMaskiertUndBereinigt()
    {
        string template = YtDlpOutputTemplate.Build(Dir, literalBaseName: "100% Speedrun: Teil 1", literalSuffix: "_high");

        Assert.Equal(@"C:\Users\x\Videos\100%% Speedrun_ Teil 1_high_%(id)s.%(ext)s", template);
    }

    [Fact]
    public void ZielordnerMitProzent_WirdMaskiert()
    {
        string template = YtDlpOutputTemplate.Build(@"D:\Downloads\100%", null, "");

        Assert.StartsWith(@"D:\Downloads\100%%\%(title)", template);
    }

    [Fact]
    public void KennzeichnungMitProzent_WirdMaskiert()
    {
        string template = YtDlpOutputTemplate.Build(Dir, null, "_50%");

        Assert.Contains("_50%%_%(id)s", template, StringComparison.Ordinal);
    }

    [Fact]
    public void ReservierterEigenerName_WirdUmbenannt()
    {
        string template = YtDlpOutputTemplate.Build(Dir, "NUL", "");

        Assert.Equal(@"C:\Users\x\Videos\NUL__%(id)s.%(ext)s", template);
    }

    [Fact]
    public void LangerEigenerName_WirdAufDasBudgetGekuerzt()
    {
        string template = YtDlpOutputTemplate.Build(Dir, new string('a', 300), "");

        string name = template[(Dir.Length + 1)..template.IndexOf("_%(id)s", StringComparison.Ordinal)];
        Assert.Equal(YtDlpOutputTemplate.MaxTitleLength, name.Length);
    }

    /// <summary>Je länger der Zielordner, desto weniger Platz bleibt unter MAX_PATH für den Titel —
    /// das Budget schrumpft, statt dass Windows das Schreiben ablehnt.</summary>
    [Fact]
    public void LangerZielordner_VerkleinertDasTitelbudget()
    {
        string longDir = @"C:\" + string.Join('\\', Enumerable.Repeat("Unterordner", 12));

        int budget = YtDlpOutputTemplate.TitleBudget(longDir, "_q1080_mp4");

        Assert.InRange(budget, YtDlpOutputTemplate.MinTitleLength, YtDlpOutputTemplate.MaxTitleLength - 1);
        int worstCase = longDir.Length + 1 + budget + "_q1080_mp4".Length + YtDlpOutputTemplate.ReservedTailLength;
        Assert.True(worstCase <= YtDlpOutputTemplate.PathLimit, $"{worstCase} > {YtDlpOutputTemplate.PathLimit}");
    }

    [Fact]
    public void ExtremLangerZielordner_BudgetFaelltNieUnterDieUntergrenze()
    {
        string hugeDir = @"C:\" + new string('x', 400);

        Assert.Equal(YtDlpOutputTemplate.MinTitleLength, YtDlpOutputTemplate.TitleBudget(hugeDir, ""));
    }

    [Fact]
    public void KurzerZielordner_VolleHoechstlaenge()
    {
        Assert.Equal(YtDlpOutputTemplate.MaxTitleLength, YtDlpOutputTemplate.TitleBudget(@"C:\V", ""));
    }

    [Fact]
    public void ZielordnerMitAbschliessendemTrenner_ErzeugtKeinenDoppeltenTrenner()
    {
        string template = YtDlpOutputTemplate.Build(@"D:\", null, "");

        Assert.StartsWith(@"D:\%(title)", template);
    }

    // --- Pfaddatei aus --print-to-file --------------------------------------------------

    /// <summary>yt-dlp schreibt UTF-8 mit <c>os.linesep</c> und hängt an — nach einem Neustart
    /// (Bandbreitenwechsel) stehen mehrere Zeilen darin; die letzte gilt.</summary>
    [Fact]
    public void TryReadFinalPath_LiestLetzteNichtLeereZeileAlsUtf8()
    {
        string file = Path.Combine(_tempDir, "final.txt");
        File.WriteAllText(file, "C:\\alt.mp4\r\nC:\\V\\Party 🎉 日本語_abc.mp4\r\n\r\n", new UTF8Encoding(false));

        Assert.Equal("C:\\V\\Party 🎉 日本語_abc.mp4", YtDlpOutputTemplate.TryReadFinalPath(file));
    }

    [Fact]
    public void TryReadFinalPath_FehlendeOderLeereDatei_LiefertNull()
    {
        string missing = Path.Combine(_tempDir, "fehlt.txt");
        string empty = Path.Combine(_tempDir, "leer.txt");
        File.WriteAllText(empty, "\r\n");

        Assert.Null(YtDlpOutputTemplate.TryReadFinalPath(missing));
        Assert.Null(YtDlpOutputTemplate.TryReadFinalPath(empty));
    }

    [Fact]
    public void NewFinalPathFile_LiegtImTempOrdnerUndIstEindeutig()
    {
        string a = YtDlpOutputTemplate.NewFinalPathFile();
        string b = YtDlpOutputTemplate.NewFinalPathFile();

        Assert.NotEqual(a, b);
        Assert.StartsWith(Path.GetTempPath(), a);
    }
}
