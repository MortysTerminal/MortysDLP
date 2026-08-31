using MortysDLP.Helpers;

namespace MortysDLP.Tests;

/// <summary>Prüft <see cref="App.TryGetUpdatedFromArgument"/> und
/// <see cref="App.ShouldSuppressUpdateOffer"/> — reine Auswertungen ohne UI/Dateisystem,
/// siehe <c>werkstatt/tasks/W3-T06.md</c>.</summary>
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
}
