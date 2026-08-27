using MortysDLP.Helpers;
using System.IO;

namespace MortysDLP.Tests;

public class AppPathsTests
{
    private static readonly string[] ReservedNames = ["CON", "PRN", "AUX", "NUL"];

    [Theory]
    [InlineData("NUL")]
    [InlineData("nul")]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("COM1")]
    [InlineData("com1")]
    [InlineData("LPT9")]
    public void SanitizeFileName_ErsetztReservierteNamenOhneEndung(string reservedName)
    {
        var ergebnis = AppPaths.SanitizeFileName(reservedName);

        Assert.NotEqual(reservedName.ToUpperInvariant(), ergebnis.ToUpperInvariant());
    }

    [Theory]
    [InlineData("CON.mp4", ".mp4")]
    [InlineData("nul.txt", ".txt")]
    public void SanitizeFileName_ErsetztReservierteNamenMitEndung(string reservedName, string extension)
    {
        var ergebnis = AppPaths.SanitizeFileName(reservedName);

        string basisName = ergebnis[..^extension.Length];
        Assert.EndsWith(extension, ergebnis);
        Assert.DoesNotContain(basisName.ToUpperInvariant(), ReservedNames);
    }

    [Theory]
    [InlineData("Titel: Untertitel")]
    [InlineData("a/b\\c*d?e\"f<g>h|i")]
    public void SanitizeFileName_EntferntUngueltigeZeichen(string eingabe)
    {
        var ergebnis = AppPaths.SanitizeFileName(eingabe);

        var invalid = Path.GetInvalidFileNameChars();
        Assert.All(ergebnis, ch => Assert.DoesNotContain(ch, invalid));
    }

    [Fact]
    public void SanitizeFileName_EntferntAbschliessendenPunkt()
    {
        var ergebnis = AppPaths.SanitizeFileName("Ende.");

        Assert.Equal("Ende", ergebnis);
    }

    [Fact]
    public void SanitizeFileName_EntferntAbschliessendesLeerzeichen()
    {
        var ergebnis = AppPaths.SanitizeFileName("Ende ");

        Assert.Equal("Ende", ergebnis);
    }

    [Fact]
    public void SanitizeFileName_EntferntMehrereAbschliessendeLeerzeichen()
    {
        var ergebnis = AppPaths.SanitizeFileName("Ende   ");

        Assert.Equal("Ende", ergebnis);
    }

    [Fact]
    public void SanitizeFileName_BegrenztDieLaenge()
    {
        string sehrLangerName = new string('a', 500);

        var ergebnis = AppPaths.SanitizeFileName(sehrLangerName, maxLength: 150);

        Assert.True(ergebnis.Length <= 150);
    }

    [Fact]
    public void SanitizeFileName_BegrenztDieLaengeAuchOhneExplizitesLimit()
    {
        string name300Zeichen = new string('a', 300);

        var ergebnis = AppPaths.SanitizeFileName(name300Zeichen);

        Assert.True(ergebnis.Length <= 150); // Standard-Höchstlänge
    }

    [Theory]
    [InlineData("Party 🎉 Zusammenfassung")]
    [InlineData("日本語のタイトル")]
    public void SanitizeFileName_LaesstEmojiUndNichtLateinischeZeichenUnveraendert(string eingabe)
    {
        var ergebnis = AppPaths.SanitizeFileName(eingabe);

        Assert.Equal(eingabe, ergebnis);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizeFileName_LeererOderNurLeerzeichenName_GibtFallbackZurueck(string eingabe)
    {
        var ergebnis = AppPaths.SanitizeFileName(eingabe);

        Assert.False(string.IsNullOrWhiteSpace(ergebnis));
    }

    [Fact]
    public void SanitizeFileName_NurPunkte_GibtFallbackZurueckStattLeeremString()
    {
        // "..." besteht ausschließlich aus abschließenden Punkten, die entfernt werden -
        // das Ergebnis darf trotzdem nie leer sein.
        var ergebnis = AppPaths.SanitizeFileName("...");

        Assert.False(string.IsNullOrEmpty(ergebnis));
    }
}
