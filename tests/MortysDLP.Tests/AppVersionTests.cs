using System.Globalization;
using MortysDLP.Models;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft Parsen, Vergleichen und Anzeigen von <see cref="AppVersion"/> — reine Logik ohne
/// Netz oder Dateisystem
/// </summary>
public class AppVersionTests
{
    [Theory]
    [InlineData("2026.06.01")]
    [InlineData("v2026.06.01")]
    [InlineData("V2026.06.01")]
    [InlineData("2026.06.01.1")]
    [InlineData("2026.6.1")]
    [InlineData("2026.6")]
    [InlineData("2026")]
    [InlineData("2026.09.01-dev.1")]
    [InlineData("  2026.06.01  ")]
    public void TryParse_GueltigeEingabe_LiefertTrue(string input)
    {
        Assert.True(AppVersion.TryParse(input, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nightly")]
    [InlineData("2026.06.01b")]
    [InlineData("2026..1")]
    [InlineData("2026.-1")]
    [InlineData("2026.06.01-")]
    [InlineData("99999999999.1")]
    [InlineData("1.2.3.4.5.6.7")]
    [InlineData("v")]
    [InlineData("2026. 6.1")]
    [InlineData("2026.+6.1")]
    [InlineData("2026.6e2.1")]
    public void TryParse_UngueltigeEingabe_LiefertFalse(string? input)
    {
        Assert.False(AppVersion.TryParse(input, out _));
    }

    [Fact]
    public void TryParse_SechsSegmente_LiefertTrue()
    {
        Assert.True(AppVersion.TryParse("1.2.3.4.5.6", out _));
    }

    [Fact]
    public void TryParse_FuehrendesV_WirdFuerRawEntfernt()
    {
        Assert.True(AppVersion.TryParse("v2026.06.01", out var version));

        Assert.Equal("2026.06.01", version.Raw);
    }

    [Fact]
    public void TryParse_Vorabsuffix_IstPrerelease()
    {
        Assert.True(AppVersion.TryParse("2026.09.01-dev.1", out var version));

        Assert.True(version.IsPrerelease);
    }

    [Fact]
    public void TryParse_OhneSuffix_IstNichtPrerelease()
    {
        Assert.True(AppVersion.TryParse("2026.06.01", out var version));

        Assert.False(version.IsPrerelease);
    }

    [Fact]
    public void Parse_GueltigeEingabe_LiefertVersion()
    {
        var version = AppVersion.Parse("2026.06.01");

        Assert.Equal("2026.06.01", version.Raw);
    }

    [Fact]
    public void Parse_UngueltigeEingabe_WirftFormatException()
    {
        Assert.Throws<FormatException>(() => AppVersion.Parse("nightly"));
    }

    [Fact]
    public void ToString_LiefertRawOhneFuehrendesV()
    {
        var version = AppVersion.Parse("V2026.06.01");

        Assert.Equal("2026.06.01", version.ToString());
    }

    [Fact]
    public void ToString_BehaeltUrspruenglicheSchreibweiseOhneFuehrendeNullen()
    {
        var version = AppVersion.Parse("2026.6.1");

        Assert.Equal("2026.6.1", version.ToString());
    }

    [Theory]
    [InlineData("2026.06.01", "2026.06.01.1")]
    [InlineData("2026.06.01", "2026.06.02")]
    [InlineData("2026.6.1", "2026.12.01")]
    [InlineData("2026.09.01-dev.1", "2026.09.01")]
    [InlineData("2026.09.01-dev.1", "2026.09.01-dev.2")]
    [InlineData("2026.09.01-dev.9", "2026.09.01-dev.10")]
    [InlineData("2026.09.01-dev.1", "2026.09.01-rc.1")]
    [InlineData("2026.6", "2026.6.1")]
    [InlineData("2026.09.01-dev.1", "2026.09.01-dev.1.1")]
    public void CompareTo_KleinereVersion_IstKleiner(string kleinere, string groessere)
    {
        var a = AppVersion.Parse(kleinere);
        var b = AppVersion.Parse(groessere);

        Assert.True(a < b);
        Assert.True(b > a);
        Assert.True(a <= b);
        Assert.True(b >= a);
        Assert.False(a == b);
    }

    [Theory]
    [InlineData("2026.06.01", "v2026.6.1")]
    [InlineData("2026.6", "2026.6.0")]
    [InlineData("2026.6.1", "2026.06.01.0")]
    public void Equals_GleichwertigeSchreibweisen_SindGleich(string x, string y)
    {
        var a = AppVersion.Parse(x);
        var b = AppVersion.Parse(y);

        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.Equal(0, a.CompareTo(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_UnterschiedlicheVersionen_SindUngleich()
    {
        var a = AppVersion.Parse("2026.06.01");
        var b = AppVersion.Parse("2026.06.02");

        Assert.False(a.Equals(b));
        Assert.True(a != b);
    }

    [Fact]
    public void Sortierung_GemischteListe_ErgibtErwarteteReihenfolge()
    {
        string[] input =
        [
            "2026.06.02",
            "2026.06.01-dev.2",
            "2026.06.01",
            "2026.06.01.1",
            "2026.06.01-dev.1",
        ];

        var sorted = input
            .Select(AppVersion.Parse)
            .OrderBy(v => v)
            .Select(v => v.ToString())
            .ToArray();

        Assert.Equal(
        [
            "2026.06.01-dev.1",
            "2026.06.01-dev.2",
            "2026.06.01",
            "2026.06.01.1",
            "2026.06.02",
        ], sorted);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    public void TryParse_UnterFremderKultur_LiefertGleichesErgebnisWieInvariant(string cultureName)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            bool parsedI = AppVersion.TryParse("2026.06.01.1", out var i);
            bool parsedV = AppVersion.TryParse("v2026.6.1", out var v);

            Assert.True(parsedI);
            Assert.True(parsedV);
            Assert.True(v < i);
            Assert.Equal("2026.06.01.1", i.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    public void TryParse_KommaAlsDezimaltrennerUnterFremderKultur_WirdWeiterhinAbgelehnt(string cultureName)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            Assert.False(AppVersion.TryParse("2026,06,01", out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    public void ComparePrereleaseIdentifier_UnterFremderKultur_BleibtOrdinal(string cultureName)
    {
        // Regressionstest gegen das türkische "İ"-Problem: ToUpper()/ToLower() ohne
        // explizite Kultur würde "i" -> "İ" wandeln und den Bezeichnervergleich verfälschen.
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            var a = AppVersion.Parse("2026.09.01-Info.1");
            var b = AppVersion.Parse("2026.09.01-info.1");

            Assert.True(a == b);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nightly")]
    [InlineData("2026.06.01b")]
    [InlineData("...")]
    [InlineData("-")]
    public void TryParse_UnbrauchbareEingabe_WirftKeineAusnahme(string? input)
    {
        var exception = Record.Exception(() => AppVersion.TryParse(input, out _));

        Assert.Null(exception);
    }

    [Fact]
    public void TryParse_ZehntausendZeichenMuell_WirftKeineAusnahmeUndLiefertFalse()
    {
        string garbage = new string('x', 10_000);

        var exception = Record.Exception(() =>
        {
            bool result = AppVersion.TryParse(garbage, out _);
            Assert.False(result);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void TryParse_ZehntausendPunkteMuell_WirftKeineAusnahmeUndLiefertFalse()
    {
        string garbage = string.Join('.', Enumerable.Repeat("1", 10_000));

        var exception = Record.Exception(() =>
        {
            bool result = AppVersion.TryParse(garbage, out _);
            Assert.False(result);
        });

        Assert.Null(exception);
    }
}
