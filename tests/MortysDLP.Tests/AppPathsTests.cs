using MortysDLP.Helpers;
using System.IO;

namespace MortysDLP.Tests;

public class AppPathsTests
{
    private static readonly string[] ReservedNames = ["CON", "PRN", "AUX", "NUL"];

    [Fact]
    public void ToolsDir_LiegtUnterDataDirNichtImProgrammverzeichnis()
    {
        // Werkzeuge müssen unter dem beschreibbaren Nutzerprofil liegen (Welle 4) - liegen sie
        // weiterhin unter AppDir, scheitert jedes Werkzeug-Update in C:\Program Files.
        Assert.StartsWith(AppPaths.DataDir + Path.DirectorySeparatorChar, AppPaths.ToolsDir);
        Assert.False(
            AppPaths.ToolsDir.StartsWith(AppPaths.AppDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "ToolsDir darf nicht mehr unter dem Programmverzeichnis liegen.");
    }

    [Fact]
    public void LegacyToolsDir_LiegtWeiterhinImProgrammverzeichnis()
    {
        // Ist die Quelle der einmaligen Übernahme - muss der alte, dokumentierte Ort bleiben.
        Assert.StartsWith(AppPaths.AppDir + Path.DirectorySeparatorChar, AppPaths.LegacyToolsDir + Path.DirectorySeparatorChar);
    }

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

    // --- Windows-Namensregeln, unabhängig vom Betriebssystem des Testlaufs ---------------
    // Quelle: learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file (2026-10-26).

    private static readonly char[] WindowsReservedChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    [Fact]
    public void SanitizeFileName_EntferntAlleWindowsReserviertenZeichenUndSteuerzeichen()
    {
        string eingabe = "a<b>c:d\"e/f\\g|h?i*j" + string.Concat(Enumerable.Range(0, 32).Select(i => (char)i)) + "k";

        var ergebnis = AppPaths.SanitizeFileName(eingabe, maxLength: 500);

        Assert.All(ergebnis, ch =>
        {
            Assert.DoesNotContain(ch, WindowsReservedChars);
            Assert.True(ch >= 32, $"Steuerzeichen {(int)ch} im Ergebnis");
        });
        Assert.StartsWith("a_b_c_d_e_f_g_h_i_j", ergebnis);
    }

    [Theory]
    [InlineData("COM\u00B9")]
    [InlineData("com\u00B2")]
    [InlineData("LPT\u00B3")]
    [InlineData("lpt\u00B9.mp4")]
    public void SanitizeFileName_ErsetztReservierteNamenMitHochgestellterZiffer(string reservedName)
    {
        var ergebnis = AppPaths.SanitizeFileName(reservedName);

        string basis = ergebnis.Split('.')[0];
        Assert.NotEqual(reservedName.Split('.')[0].ToUpperInvariant(), basis.ToUpperInvariant());
    }

    /// <summary>Windows behandelt <c>NUL.tar.gz</c> wie <c>NUL</c> — maßgeblich ist der Teil vor
    /// dem <b>ersten</b> Punkt, nicht vor dem letzten.</summary>
    [Theory]
    [InlineData("NUL.tar.gz", "NUL_.tar.gz")]
    [InlineData("con.part.mp4", "con_.part.mp4")]
    [InlineData("AUX.", "AUX_")]
    [InlineData("CON .txt", "CON_ .txt")]
    public void SanitizeFileName_ReservierterNameMitMehrerenEndungen_WirdErkannt(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, AppPaths.SanitizeFileName(eingabe));
    }

    [Theory]
    [InlineData("CONSOLE")]
    [InlineData("NULL.txt")]
    [InlineData("COM10")]
    [InlineData("Concert.mp4")]
    public void SanitizeFileName_AehnlicheAberNichtReservierteNamen_BleibenUnveraendert(string eingabe)
    {
        Assert.Equal(eingabe, AppPaths.SanitizeFileName(eingabe));
    }

    /// <summary>Ein halbiertes Surrogatpaar wäre ungültiges UTF-16 — manche Programme lehnen den
    /// Namen dann ab oder ersetzen das Zeichen.</summary>
    [Fact]
    public void SanitizeFileName_KuerzenHalbiertKeinEmoji()
    {
        string eingabe = new string('a', 9) + "🎉🎉";

        var ergebnis = AppPaths.SanitizeFileName(eingabe, maxLength: 10);

        Assert.Equal(new string('a', 9), ergebnis);
        Assert.False(char.IsHighSurrogate(ergebnis[^1]));
    }

    [Fact]
    public void SanitizeFileName_GekuerzterNameEndetNichtMitPunktOderLeerzeichen()
    {
        var ergebnis = AppPaths.SanitizeFileName("abcde. fghij", maxLength: 7);

        Assert.Equal("abcde", ergebnis);
    }
}
