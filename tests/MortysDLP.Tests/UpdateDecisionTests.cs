using MortysDLP.Models;
using MortysDLP.Services;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="UpdateDecision.ShouldOffer"/> — reine Logik, kein Netz- oder
/// Dateizugriff.
/// </summary>
public class UpdateDecisionTests
{
    private static readonly AppVersion Current = AppVersion.Parse("2026.06.01");

    [Fact]
    public void ShouldOffer_GleicheVersion_KeinAngebot()
    {
        Assert.False(UpdateDecision.ShouldOffer(Current, Current, null));
    }

    [Fact]
    public void ShouldOffer_AeltereNeuesteVersion_KeinAngebot()
    {
        var latest = AppVersion.Parse("2026.05.01");

        Assert.False(UpdateDecision.ShouldOffer(Current, latest, null));
    }

    [Fact]
    public void ShouldOffer_NeuereVersionNichtsUebersprungen_Angebot()
    {
        var latest = AppVersion.Parse("2026.07.01");

        Assert.True(UpdateDecision.ShouldOffer(Current, latest, null));
    }

    [Fact]
    public void ShouldOffer_NeuesteGleichUebersprungeneVersion_KeinAngebot()
    {
        var latest = AppVersion.Parse("2026.07.01");

        Assert.False(UpdateDecision.ShouldOffer(Current, latest, "2026.07.01"));
    }

    [Fact]
    public void ShouldOffer_VersionNeuerAlsUebersprungene_Angebot()
    {
        var latest = AppVersion.Parse("2026.09.01");

        Assert.True(UpdateDecision.ShouldOffer(Current, latest, "2026.07.01"));
    }

    [Fact]
    public void ShouldOffer_VersionAelterAlsUebersprungeneAberNeuerAlsEigene_KeinAngebot()
    {
        // Übersprungen: 2026.09.01. Eine Quelle meldet zwischenzeitlich nur 2026.07.01
        // (neuer als die laufende 2026.06.01, aber älter als das Übersprungene).
        var latest = AppVersion.Parse("2026.07.01");

        Assert.False(UpdateDecision.ShouldOffer(Current, latest, "2026.09.01"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nightly")]
    public void ShouldOffer_SkippedUnlesbarOderLeer_Angebot(string? skipped)
    {
        var latest = AppVersion.Parse("2026.07.01");

        Assert.True(UpdateDecision.ShouldOffer(Current, latest, skipped));
    }

    [Fact]
    public void ShouldOffer_UnterschiedlicheSchreibweisenDesUebersprungenenWerts_AlsGleichErkannt()
    {
        // "2026.7.1" (führende Nullen weggelassen) beschreibt dieselbe Version wie
        // "v2026.07.01" - beide neuer als Current, damit der erste Check (latest <= current)
        // nicht schon vorher entscheidet.
        var latest = AppVersion.Parse("2026.7.1");

        Assert.False(UpdateDecision.ShouldOffer(Current, latest, "v2026.07.01"));
    }
}
