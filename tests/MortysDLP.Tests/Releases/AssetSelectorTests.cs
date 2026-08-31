using MortysDLP.Services.Releases;

namespace MortysDLP.Tests.Releases;

/// <summary>
/// Prüft <see cref="AssetSelector"/> — reine Logik, kein Netzzugriff.
/// </summary>
public class AssetSelectorTests
{
    private const string Pattern = "MortysDLP*.zip";

    private static ReleaseAsset MakeAsset(string name) => new(name, $"https://example.invalid/{name}", 1000);

    [Fact]
    public void Select_EinTreffer_LiefertIhn()
    {
        ReleaseAsset[] assets = [MakeAsset("MortysDLP.zip"), MakeAsset("checksums.txt")];

        var result = AssetSelector.Select(assets, Pattern);

        Assert.NotNull(result);
        Assert.Equal("MortysDLP.zip", result!.Name);
    }

    [Fact]
    public void Select_MehrereTrefferMitExakterUebereinstimmung_BevorzugtDiese()
    {
        ReleaseAsset[] assets = [MakeAsset("MortysDLP-portable.zip"), MakeAsset("MortysDLP.zip")];

        var result = AssetSelector.Select(assets, Pattern);

        Assert.NotNull(result);
        Assert.Equal("MortysDLP.zip", result!.Name);
    }

    [Fact]
    public void Select_MehrereTrefferOhneExakteUebereinstimmung_WirftAssetAmbiguousException()
    {
        ReleaseAsset[] assets = [MakeAsset("MortysDLP-x64.zip"), MakeAsset("MortysDLP-x86.zip")];

        var ex = Assert.Throws<AssetAmbiguousException>(() => AssetSelector.Select(assets, Pattern));

        Assert.Contains("MortysDLP-x64.zip", ex.CandidateNames);
        Assert.Contains("MortysDLP-x86.zip", ex.CandidateNames);
    }

    [Fact]
    public void Select_KeinTreffer_LiefertNull()
    {
        ReleaseAsset[] assets = [MakeAsset("readme.txt"), MakeAsset("checksums.txt")];

        Assert.Null(AssetSelector.Select(assets, Pattern));
    }

    [Fact]
    public void Select_GrossKleinschreibungWirdIgnoriert()
    {
        ReleaseAsset[] assets = [MakeAsset("MORTYSDLP.ZIP")];

        var result = AssetSelector.Select(assets, Pattern);

        Assert.NotNull(result);
        Assert.Equal("MORTYSDLP.ZIP", result!.Name);
    }

    [Fact]
    public void Select_ChecksumsTxtWirdNieGewaehlt()
    {
        ReleaseAsset[] assets = [MakeAsset("checksums.txt")];

        Assert.Null(AssetSelector.Select(assets, "*"));
    }

    [Fact]
    public void Select_LeereListe_LiefertNull()
    {
        Assert.Null(AssetSelector.Select([], Pattern));
    }
}
