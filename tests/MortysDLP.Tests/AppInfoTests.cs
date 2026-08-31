using MortysDLP.Helpers;
using MortysDLP.Models;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft, dass die aus der Assembly gelesene Version brauchbar ist und dass die
/// SourceLink-Abschneide-Logik ("+&lt;sha&gt;") für sich testbar ist.
/// </summary>
public class AppInfoTests
{
    [Fact]
    public void Current_IstNichtLeerUndEnthaeltKeinPlus()
    {
        Assert.False(string.IsNullOrEmpty(AppInfo.Current));
        Assert.DoesNotContain('+', AppInfo.Current);
    }

    [Fact]
    public void Current_LaesstSichAlsAppVersionLesen()
    {
        Assert.True(AppVersion.TryParse(AppInfo.Current, out _));
    }

    [Fact]
    public void CurrentVersion_EntsprichtDemGeparstenCurrent()
    {
        Assert.NotNull(AppInfo.CurrentVersion);
        Assert.Equal(AppVersion.Parse(AppInfo.Current!), AppInfo.CurrentVersion!.Value);
    }

    [Theory]
    [InlineData("2026.06.01", "2026.06.01")]
    [InlineData("2026.06.01+abc123", "2026.06.01")]
    [InlineData("  2026.06.01  ", "2026.06.01")]
    [InlineData("  2026.06.01+abc123  ", "2026.06.01")]
    public void StripSourceLinkSuffix_MitInhalt_EntferntNurDenHashAnteil(string input, string expected)
    {
        Assert.Equal(expected, AppInfo.StripSourceLinkSuffix(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StripSourceLinkSuffix_LeereOderFehlendeEingabe_LiefertNull(string? input)
    {
        Assert.Null(AppInfo.StripSourceLinkSuffix(input));
    }

    [Fact]
    public void StripSourceLinkSuffix_NurPlusOhneRest_LiefertNull()
    {
        Assert.Null(AppInfo.StripSourceLinkSuffix("+abc123"));
    }
}
