using MortysDLP.Helpers;

namespace MortysDLP.Tests;

public class FontAppearanceTests
{
    [Fact]
    public void All_ArialZuerst_InterUndComicSansDrin()
    {
        Assert.Equal("arial", FontAppearance.All[0]);
        Assert.Contains("inter", FontAppearance.All);
        Assert.Contains("compact", FontAppearance.All);
        Assert.Contains("comicsans", FontAppearance.All);
        Assert.True(FontAppearance.All.Count >= 8);
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
    [InlineData("inter", "inter")]
    [InlineData("comicsans", "comicsans")]
    [InlineData("standard", "arial")]   // Alt-Wert von vor 2026-09-08
    [InlineData("gibt-es-nicht", "arial")]
    [InlineData("", "arial")]
    [InlineData(null, "arial")]
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
