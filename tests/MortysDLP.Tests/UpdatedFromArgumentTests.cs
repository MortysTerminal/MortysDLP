using MortysDLP.Helpers;

namespace MortysDLP.Tests;

/// <summary>Prüft <see cref="App.TryGetUpdatedFromArgument"/> und
/// <see cref="App.ShouldSuppressUpdateOffer"/> — reine Auswertungen ohne UI/Dateisystem,
///</summary>
public class UpdatedFromArgumentTests
{
    [Fact]
    public void TryGetUpdatedFromArgument_Vorhanden_LiefertWert()
    {
        string[] args = ["MortysDLP.exe", "--updated-from", "2026.06.01"];

        string? result = App.TryGetUpdatedFromArgument(args);

        Assert.Equal("2026.06.01", result);
    }

    [Fact]
    public void TryGetUpdatedFromArgument_NichtVorhanden_LiefertNull()
    {
        string[] args = ["MortysDLP.exe"];

        string? result = App.TryGetUpdatedFromArgument(args);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetUpdatedFromArgument_AmEndeOhneWert_LiefertNull()
    {
        string[] args = ["MortysDLP.exe", "--updated-from"];

        string? result = App.TryGetUpdatedFromArgument(args);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetUpdatedFromArgument_LeeresArray_LiefertNull()
    {
        string? result = App.TryGetUpdatedFromArgument([]);

        Assert.Null(result);
    }

    [Fact]
    public void ShouldSuppressUpdateOffer_ReadOnly_LiefertTrue() =>
        Assert.True(App.ShouldSuppressUpdateOffer(InstallKind.ReadOnly));

    [Fact]
    public void ShouldSuppressUpdateOffer_RunningFromArchive_LiefertTrue() =>
        Assert.True(App.ShouldSuppressUpdateOffer(InstallKind.RunningFromArchive));

    [Fact]
    public void ShouldSuppressUpdateOffer_Writable_LiefertFalse() =>
        Assert.False(App.ShouldSuppressUpdateOffer(InstallKind.Writable));

    [Fact]
    public void ShouldSuppressUpdateOffer_NeedsElevation_LiefertFalse() =>
        Assert.False(App.ShouldSuppressUpdateOffer(InstallKind.NeedsElevation));

    [Fact]
    public void IsVersionChangeConfirmed_NeuereVersionLaeuft_LiefertTrue() =>
        Assert.True(App.IsVersionChangeConfirmed("2026.06.01", "2026.09.01"));

    [Fact]
    public void IsVersionChangeConfirmed_GleicheVersion_LiefertFalse()
    {
        // Der Updater startet die App auch dann mit --updated-from neu, wenn er keine einzige
        // Datei ersetzt hat. Ohne diese Prüfung meldete die App dann einen Erfolg, den es
        // nicht gab.
        Assert.False(App.IsVersionChangeConfirmed("2026.06.01", "2026.06.01"));
    }

    [Theory]
    [InlineData("v2026.06.01", "2026.6.1")]
    [InlineData("2026.06.01", "2026.06.01.0")]
    [InlineData(" 2026.06.01 ", "2026.06.01")]
    public void IsVersionChangeConfirmed_GleicheVersionAndersGeschrieben_LiefertFalse(
        string updatedFrom, string current)
    {
        // Führende Nullen, ein "v"-Präfix oder ein weggelassenes Nullsegment sind keine
        // Versionsänderung — ohne den Vergleich über AppVersion gälten sie als eine.
        Assert.False(App.IsVersionChangeConfirmed(updatedFrom, current));
    }

    [Theory]
    [InlineData(null, "2026.09.01")]
    [InlineData("2026.06.01", null)]
    [InlineData("", "2026.09.01")]
    [InlineData("   ", "2026.09.01")]
    public void IsVersionChangeConfirmed_UnbrauchbareAngabe_LiefertFalse(string? updatedFrom, string? current)
    {
        // Eine unsichere Erfolgsmeldung ist schlimmer als keine.
        Assert.False(App.IsVersionChangeConfirmed(updatedFrom, current));
    }

    [Fact]
    public void IsVersionChangeConfirmed_NichtParsbareAberVerschiedeneAngaben_LiefertTrue()
    {
        // Kein AppVersion-Vergleich möglich -> Rückfall auf den Zeichenkettenvergleich.
        Assert.True(App.IsVersionChangeConfirmed("nightly-alt", "nightly-neu"));
    }
}
