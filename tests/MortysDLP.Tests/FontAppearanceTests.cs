using MortysDLP.Helpers;

namespace MortysDLP.Tests;

public class FontAppearanceTests
{
    [Fact]
    public void All_EnthaeltInterAlsErstenUndMehrereVarianten()
    {
        Assert.Equal("inter", FontAppearance.All[0]);
        Assert.Contains("compact", FontAppearance.All);
        Assert.Contains("airy", FontAppearance.All);
        Assert.True(FontAppearance.All.Count >= 5);
    }

    [Fact]
    public void All_EnthaeltKeineDoppelten()
    {
        Assert.Equal(FontAppearance.All.Count, FontAppearance.All.Distinct().Count());
    }

    [Theory]
    [InlineData("compact", "compact")]
    [InlineData("airy", "airy")]
    [InlineData("verdana", "verdana")]
    [InlineData("standard", "inter")]   // Alt-Wert von vor 2026-09-08
    [InlineData("gibt-es-nicht", "inter")]
    [InlineData("", "inter")]
    [InlineData(null, "inter")]
    public void Current_NormalisiertUndBildetAltwerteAb(string? stored, string expected)
    {
        string original = MortysDLP.Properties.Settings.Default.FontAppearance;
        try
        {
            MortysDLP.Properties.Settings.Default.FontAppearance = stored;
            Assert.Equal(expected, FontAppearance.Current);
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
        Assert.Null(Record.Exception(() => FontAppearance.Apply("verdana")));
        Assert.Null(Record.Exception(() => FontAppearance.Apply("gibt-es-nicht")));
    }
}
