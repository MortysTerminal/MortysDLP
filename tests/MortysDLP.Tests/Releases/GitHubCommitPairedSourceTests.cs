using MortysDLP.Services.Releases;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace MortysDLP.Tests.Releases;

/// <summary>
/// Prüft <see cref="GitHubCommitPairedSource"/>: die Zuordnung eines stabilen Versions-Tags zu dem
/// Release, das über denselben Commit die Programmpakete trägt, und den Rückgriff auf das ältere
/// Schema, bei dem das Versions-Release die Pakete selbst trägt. Kein Test greift auf das echte
/// Netz zu.
/// </summary>
public class GitHubCommitPairedSourceTests
{
    private const string AssetName = "whisper-blas-bin-x64.zip";

    private static readonly ReleaseQuery Query = new("ggml-org", "whisper.cpp", AssetPattern: AssetName);

    /// <summary>Echte Antwort von <c>GET /repos/ggml-org/whisper.cpp/releases</c> vom 2026-10-26,
    /// auf die ausgewerteten Felder und zwei Anhänge je Release gekürzt — nicht erfunden.</summary>
    private const string RealListJson = """
        [
          {"assets":[],"draft":false,"prerelease":false,"tag_name":"v1.9.5","target_commitish":"d1be6fde11ac6e0407606b4e42fe72d34add8037"},
          {"assets":[
            {"browser_download_url":"https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-bin-x64.zip","digest":"sha256:6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb","name":"whisper-bin-x64.zip","size":8928640},
            {"browser_download_url":"https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-blas-bin-x64.zip","digest":"sha256:b1158bd10edf92c9799149a6fc5d3e58da3f495241d6fb3423088130e88ffbf3","name":"whisper-blas-bin-x64.zip","size":21715607}
          ],"draft":false,"prerelease":true,"tag_name":"b5454","target_commitish":"d1be6fde11ac6e0407606b4e42fe72d34add8037"},
          {"assets":[],"draft":false,"prerelease":false,"tag_name":"v1.9.4","target_commitish":"927cfce34f31707e17f2bff35c349632fb9e2c3a"},
          {"assets":[
            {"browser_download_url":"https://github.com/ggml-org/whisper.cpp/releases/download/b5130/whisper-blas-bin-x64.zip","digest":"sha256:55c06d09e8b9b6cfb2b0b47ddedc71803054f0e48be1f41848b3141c06c703a9","name":"whisper-blas-bin-x64.zip","size":21360234}
          ],"draft":false,"prerelease":true,"tag_name":"b5130","target_commitish":"927cfce34f31707e17f2bff35c349632fb9e2c3a"},
          {"assets":[
            {"browser_download_url":"https://github.com/ggml-org/whisper.cpp/releases/download/b5127/whisper-blas-bin-x64.zip","digest":"sha256:10426b8751f71e8301e5da5e6d0c81d3d1eac766313ab163368cbd2059015891","name":"whisper-blas-bin-x64.zip","size":21360231}
          ],"draft":false,"prerelease":true,"tag_name":"b5127","target_commitish":"a2b36eb677918d4f9ab1db7b8a7ff968563ed163"},
          {"assets":[],"draft":false,"prerelease":true,"tag_name":"v1.9.3","target_commitish":"371b5a7561823ab2bb32142d2751e35e7534727b"}
        ]
        """;

    private const string ShaA = "1111111111111111111111111111111111111111";
    private const string ShaB = "2222222222222222222222222222222222222222";

    private const string SomeDigest = "sha256:ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12";

    private static string Release(string tag, string commitish, bool prerelease = false, bool draft = false,
        string? assetName = null, string digest = SomeDigest)
    {
        string asset = assetName is null
            ? ""
            : $$"""{"name":"{{assetName}}","size":1234,"digest":"{{digest}}","browser_download_url":"https://github.com/o/r/releases/download/{{tag}}/{{assetName}}"}""";

        return $$"""{"tag_name":"{{tag}}","target_commitish":"{{commitish}}","prerelease":{{(prerelease ? "true" : "false")}},"draft":{{(draft ? "true" : "false")}},"assets":[{{asset}}]}""";
    }

    private static ReleaseInfo? Parse(params string[] releases) =>
        new GitHubCommitPairedSource().ParsePairedRelease("[" + string.Join(",", releases) + "]", Query);

    [Fact]
    public void EchteAntwort_LiefertVersionDesTagsMitPaketUndPruefsummeDesBuilds()
    {
        var info = new GitHubCommitPairedSource().ParsePairedRelease(RealListJson, Query);

        Assert.NotNull(info);
        Assert.Equal("1.9.5", info!.Version.ToString());
        Assert.Equal("https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-blas-bin-x64.zip", info.DownloadUrl);
        Assert.Equal(21715607, info.ExpectedSize);
        Assert.Equal("b1158bd10edf92c9799149a6fc5d3e58da3f495241d6fb3423088130e88ffbf3", info.Sha256);
    }

    /// <summary>Älteres Schema, wie es z. B. <c>v1.8.3</c> zeigt: Das Versions-Release trägt die
    /// Pakete selbst und als Ziel nur <c>master</c> — über den Commit ließe es sich nicht zuordnen.</summary>
    [Fact]
    public void AltesSchema_PaketAmVersionsReleaseMitBranchZiel_WirdDirektGenommen()
    {
        var info = Parse(Release("v1.8.3", "master", assetName: AssetName));

        Assert.NotNull(info);
        Assert.Equal("1.8.3", info!.Version.ToString());
        Assert.Equal($"https://github.com/o/r/releases/download/v1.8.3/{AssetName}", info.DownloadUrl);
    }

    [Fact]
    public void EigenerAnhangDesVersionsReleases_GehtVorBuildMitGleichemCommit()
    {
        var info = Parse(
            Release("b5454", ShaA, prerelease: true, assetName: AssetName),
            Release("v1.9.5", ShaA, assetName: AssetName));

        Assert.Equal($"https://github.com/o/r/releases/download/v1.9.5/{AssetName}", info!.DownloadUrl);
    }

    [Fact]
    public void CommitHash_WirdOhneGrossKleinschreibungVerglichen()
    {
        var info = Parse(
            Release("v1.9.5", ShaA.Replace('1', 'A')),
            Release("b5454", ShaA.Replace('1', 'a'), prerelease: true, assetName: AssetName));

        Assert.NotNull(info);
    }

    [Fact]
    public void Ergebnis_FuehrtChangelogDesVersionsReleasesUndDenGewaehltenAnhang()
    {
        string versionRelease = Release("v1.9.5", ShaA).Replace("\"assets\":[]", "\"assets\":[],\"body\":\"Neu in 1.9.5\"");

        var info = Parse(versionRelease, Release("b5454", ShaA, prerelease: true, assetName: AssetName));

        Assert.Equal("Neu in 1.9.5", info!.Changelog);
        var asset = Assert.Single(info.Assets);
        Assert.Equal(AssetName, asset.Name);
        Assert.Equal(info.DownloadUrl, asset.Url);
    }

    [Fact]
    public void ZielIstBranchName_WirdNieZugeordnet()
    {
        // Ältere Releases tragen "master" statt eines Commit-Hashs - zwei solche Releases haben
        // nichts miteinander zu tun.
        var info = Parse(
            Release("v1.8.3", "master"),
            Release("b3000", "master", prerelease: true, assetName: AssetName));

        Assert.Null(info);
    }

    [Fact]
    public void NeuesteVersionOhnePaket_FaelltAufNaechstAeltereZugeordneteZurueck()
    {
        var info = Parse(
            Release("v1.9.6", ShaB),
            Release("v1.9.5", ShaA),
            Release("b5454", ShaA, prerelease: true, assetName: AssetName));

        Assert.NotNull(info);
        Assert.Equal("1.9.5", info!.Version.ToString());
    }

    /// <summary>GitHub sortiert nach Veröffentlichungsdatum, nicht nach Version — ein
    /// nachgereichtes Wartungs-Release einer älteren Linie darf nicht gewinnen.</summary>
    [Fact]
    public void UnsortierteListe_HoechsteVersionGewinnt()
    {
        var info = Parse(
            Release("v1.8.8", ShaB),
            Release("b6000", ShaB, prerelease: true, assetName: AssetName),
            Release("v1.9.5", ShaA),
            Release("b5454", ShaA, prerelease: true, assetName: AssetName));

        Assert.Equal("1.9.5", info!.Version.ToString());
    }

    [Fact]
    public void VorabversionAlsVersionstraeger_WirdOhneAllowPrereleaseIgnoriert()
    {
        var info = Parse(
            Release("v2.0.0-rc.1", ShaB, prerelease: true, assetName: AssetName),
            Release("v1.9.5", ShaA),
            Release("b5454", ShaA, prerelease: true, assetName: AssetName));

        Assert.Equal("1.9.5", info!.Version.ToString());
    }

    [Fact]
    public void EntwurfMitPassendemCommit_WirdNichtGenommen()
    {
        var info = Parse(
            Release("v1.9.5", ShaA),
            Release("b5454", ShaA, prerelease: true, draft: true, assetName: AssetName));

        Assert.Null(info);
    }

    [Fact]
    public void BuildMitAnderemAnhang_ZaehltNicht()
    {
        var info = Parse(
            Release("v1.9.5", ShaA),
            Release("b5454", ShaA, prerelease: true, assetName: "whisper-bin-x64.zip"));

        Assert.Null(info);
    }

    [Theory]
    [InlineData("sha256:ABCDEF")]
    [InlineData("sha512:b1158bd10edf92c9799149a6fc5d3e58da3f495241d6fb3423088130e88ffbf3")]
    [InlineData("b1158bd10edf92c9799149a6fc5d3e58da3f495241d6fb3423088130e88ffbf3")]
    [InlineData("")]
    public void UnbrauchbarerDigest_LiefertAdresseOhnePruefsumme(string digest)
    {
        var info = Parse(
            Release("v1.9.5", ShaA),
            Release("b5454", ShaA, prerelease: true, assetName: AssetName, digest: digest));

        Assert.NotNull(info);
        Assert.NotNull(info!.DownloadUrl);
        Assert.Null(info.Sha256);
    }

    [Fact]
    public void GrossgeschriebenerDigest_WirdKleingeschriebenUebernommen()
    {
        var info = Parse(
            Release("v1.9.5", ShaA),
            Release("b5454", ShaA, prerelease: true, assetName: AssetName,
                digest: "sha256:B1158BD10EDF92C9799149A6FC5D3E58DA3F495241D6FB3423088130E88FFBF3"));

        Assert.Equal("b1158bd10edf92c9799149a6fc5d3e58da3f495241d6fb3423088130e88ffbf3", info!.Sha256);
    }

    [Fact]
    public void OhneAnhangsmuster_LiefertNull()
    {
        var info = new GitHubCommitPairedSource().ParsePairedRelease(RealListJson, Query with { AssetPattern = null });

        Assert.Null(info);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[nicht valides json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("""[{"tag_name":"b5454"}]""")]
    public void UnbrauchbarerInhalt_LiefertNull(string json)
    {
        Assert.Null(new GitHubCommitPairedSource().ParsePairedRelease(json, Query));
    }

    [Fact]
    public void IstAuthoritative()
    {
        Assert.True(new GitHubCommitPairedSource().IsAuthoritative);
    }

    [Fact]
    public async Task ErfolgreicheAntwort_FragtDreissigEintraegeAbUndUebernimmtETag()
    {
        var headers = new Dictionary<string, string> { ["ETag"] = "\"neu456\"" };
        var handler = new FakeHttpMessageHandler()
            .When(@"repos/ggml-org/whisper\.cpp/releases\?per_page=30$", content: RealListJson, headers: headers);
        using var client = new HttpClient(handler);

        var info = await new GitHubCommitPairedSource(client).TryGetLatestAsync(Query, CancellationToken.None);

        Assert.NotNull(info);
        Assert.Equal("1.9.5", info!.Version.ToString());
        Assert.Equal("\"neu456\"", info.ETag);
        Assert.False(info.NotModified);
    }

    [Fact]
    public async Task Antwort304_LiefertNotModifiedMitEcho()
    {
        var handler = new FakeHttpMessageHandler()
            .When(@"releases\?per_page=30$", status: HttpStatusCode.NotModified);
        using var client = new HttpClient(handler);

        var info = await new GitHubCommitPairedSource(client)
            .TryGetLatestAsync(Query with { ETag = "\"abc123\"" }, CancellationToken.None);

        Assert.NotNull(info);
        Assert.True(info!.NotModified);
        Assert.Equal("\"abc123\"", info.ETag);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task FehlerStatus_LiefertNullOhneWurf(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler().When(@"releases\?per_page=30$", status: status);
        using var client = new HttpClient(handler);

        var info = await new GitHubCommitPairedSource(client).TryGetLatestAsync(Query, CancellationToken.None);

        Assert.Null(info);
    }
}
