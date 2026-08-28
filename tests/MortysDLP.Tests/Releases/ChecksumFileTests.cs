using MortysDLP.Services.Releases;

namespace MortysDLP.Tests.Releases;

/// <summary>
/// Prüft <see cref="ChecksumFile"/> gegen die Toleranzen von <c>sha256sum</c>/
/// <c>Get-FileHash</c>-Ausgaben — reine Logik, kein Netzzugriff. Siehe
/// <c>werkstatt/tasks/W2-T07.md</c>.
/// </summary>
public class ChecksumFileTests
{
    private const string Sha1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Sha2 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void Parse_NormaleZeile_LiefertEintrag()
    {
        var result = ChecksumFile.Parse($"{Sha1}  MortysDLP.zip\n");

        Assert.Equal(Sha1, result["MortysDLP.zip"]);
    }

    [Fact]
    public void Parse_SternchenForm_EntferntSternchenAusDemDateinamen()
    {
        var result = ChecksumFile.Parse($"{Sha1} *MortysDLP.zip\n");

        Assert.Equal(Sha1, result["MortysDLP.zip"]);
    }

    [Fact]
    public void Parse_LeerzeilenUndKommentare_WerdenUebersprungen()
    {
        var result = ChecksumFile.Parse($"# Kommentarzeile\n\n{Sha1}  a.zip\n{Sha2}  b.zip\n\n# Ende\n");

        Assert.Equal(2, result.Count);
        Assert.Equal(Sha1, result["a.zip"]);
        Assert.Equal(Sha2, result["b.zip"]);
    }

    [Fact]
    public void Parse_CRLF_WirdWieLFBehandelt()
    {
        var result = ChecksumFile.Parse($"{Sha1}  a.zip\r\n{Sha2}  b.zip\r\n");

        Assert.Equal(2, result.Count);
        Assert.Equal(Sha1, result["a.zip"]);
        Assert.Equal(Sha2, result["b.zip"]);
    }

    [Fact]
    public void Parse_MuellzeileOhneTrenner_WirdUebersprungen()
    {
        var result = ChecksumFile.Parse($"garantiert-keine-gueltige-zeile-ohne-leerraum\n{Sha1}  gueltig.zip\n");

        Assert.Single(result);
        Assert.Equal(Sha1, result["gueltig.zip"]);
    }

    [Fact]
    public void Parse_UngueltigeShaLaenge_WirdUebersprungen()
    {
        var result = ChecksumFile.Parse("abc123  datei.zip\n");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NichtHexadezimaleZeichen_WirdUebersprungen()
    {
        string invalidSha = "g" + new string('a', 63);
        var result = ChecksumFile.Parse($"{invalidSha}  datei.zip\n");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_GrossgeschriebenePruefsumme_WirdKleinNormalisiert()
    {
        var result = ChecksumFile.Parse($"{Sha1.ToUpperInvariant()}  a.zip\n");

        Assert.Equal(Sha1, result["a.zip"]);
    }

    [Fact]
    public void Parse_LeererInhalt_LiefertLeeresErgebnis()
    {
        Assert.Empty(ChecksumFile.Parse(""));
    }

    [Fact]
    public void Parse_BeliebigerLeerraumAlsTrenner_WirdToleriert()
    {
        var result = ChecksumFile.Parse($"{Sha1}\t\t  a.zip\n");

        Assert.Equal(Sha1, result["a.zip"]);
    }

    [Fact]
    public void Find_DateinameOhneGrossKleinschreibung_LiefertPruefsumme()
    {
        string content = $"{Sha1}  MortysDLP.zip\n";

        Assert.Equal(Sha1, ChecksumFile.Find(content, "mortysdlp.ZIP"));
    }

    [Fact]
    public void Find_UnbekannteDatei_LiefertNull()
    {
        string content = $"{Sha1}  a.zip\n";

        Assert.Null(ChecksumFile.Find(content, "b.zip"));
    }
}
