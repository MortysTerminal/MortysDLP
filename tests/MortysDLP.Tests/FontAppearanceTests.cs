using MortysDLP.Helpers;

namespace MortysDLP.Tests;

public class FontAppearanceTests
{
    [Fact]
    public void All_EnthaeltGenauDieDreiWerte()
    {
        Assert.Equal(
            new[] { FontAppearance.Compact, FontAppearance.Standard, FontAppearance.Airy },
            FontAppearance.All);
    }

    [Fact]
    public void Konstanten_SindKleingeschriebeneStrings()
    {
        Assert.Equal("compact", FontAppearance.Compact);
        Assert.Equal("standard", FontAppearance.Standard);
        Assert.Equal("airy", FontAppearance.Airy);
    }

    [Fact]
    public void Current_UnbekannterWert_WirdZuStandard()
    {
        string original = MortysDLP.Properties.Settings.Default.FontAppearance;
        try
        {
            MortysDLP.Properties.Settings.Default.FontAppearance = "gibt-es-nicht";
            Assert.Equal(FontAppearance.Standard, FontAppearance.Current);

            MortysDLP.Properties.Settings.Default.FontAppearance = FontAppearance.Airy;
            Assert.Equal(FontAppearance.Airy, FontAppearance.Current);
        }
        finally
        {
            MortysDLP.Properties.Settings.Default.FontAppearance = original;
        }
    }

    [Fact]
    public void Apply_OhneApplication_WirftNicht()
    {
        // In der Testumgebung gibt es keine WPF-Application - Apply muss das still hinnehmen.
        var ex = Record.Exception(() => FontAppearance.Apply(FontAppearance.Airy));
        Assert.Null(ex);
    }
}
