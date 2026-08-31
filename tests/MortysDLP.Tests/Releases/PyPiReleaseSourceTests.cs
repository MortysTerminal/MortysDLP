using MortysDLP.Services.Releases;
using System.Net;
using System.Net.Http;

namespace MortysDLP.Tests.Releases;

/// <summary>
/// Prüft <see cref="PyPiReleaseSource"/> — reine Logik über <c>ParsePyPiJson</c> und die volle
/// <c>TryGetLatestAsync</c>-Kette über <see cref="FakeHttpMessageHandler"/>. Kein Test greift
/// auf das echte Netz zu.
/// </summary>
public class PyPiReleaseSourceTests
{
    private const string RealisticJson = """
        {
          "info": {
            "name": "yt-dlp",
            "version": "2026.08.19"
          },
          "releases": {}
        }
        """;

    private static readonly ReleaseQuery Query = new("yt-dlp", "yt-dlp", PackageName: "yt-dlp");

    [Fact]
    public void ParsePyPiJson_RealistischeAntwort_LiefertVersion()
    {
        var info = new PyPiReleaseSource().ParsePyPiJson(RealisticJson);

        Assert.NotNull(info);
        Assert.Equal("2026.08.19", info!.Version.ToString());
    }

    [Fact]
    public void IsAuthoritative_IstFalse()
    {
        // PyPI und der GitHub-Release müssen nicht im selben Moment erscheinen - eine
        // "gleich oder älter"-Antwort ist deshalb kein Beweis für "kein Update".
        Assert.False(new PyPiReleaseSource().IsAuthoritative);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{nicht valides json")]
    [InlineData("{}")]
    [InlineData("""{"info": {}}""")]
    [InlineData("""{"info": {"version": "nicht-lesbar!"}}""")]
    public void ParsePyPiJson_UnbrauchbarerInhalt_LiefertNull(string json)
    {
        Assert.Null(new PyPiReleaseSource().ParsePyPiJson(json));
    }

    [Fact]
    public async Task TryGetLatestAsync_OhnePackageName_LiefertNullOhneAnfrage()
    {
        var handler = new FakeHttpMessageHandler();
        using var client = new HttpClient(handler);
        var source = new PyPiReleaseSource(client);

        var info = await source.TryGetLatestAsync(new ReleaseQuery("x", "y"), CancellationToken.None);

        Assert.Null(info);
        Assert.Equal(0, handler.TotalCallCount);
    }

    [Fact]
    public async Task TryGetLatestAsync_ErfolgreicheAntwort_LiestVersionAusPypiJson()
    {
        var handler = new FakeHttpMessageHandler().When(@"pypi\.org/pypi/yt-dlp/json$", content: RealisticJson);
        using var client = new HttpClient(handler);
        var source = new PyPiReleaseSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.NotNull(info);
        Assert.Equal("2026.08.19", info!.Version.ToString());
        Assert.Equal("pypi", info.SourceName);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task TryGetLatestAsync_FehlerStatus_LiefertNullOhneWurf(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler().When(@"pypi\.org/pypi/yt-dlp/json$", status: status);
        using var client = new HttpClient(handler);
        var source = new PyPiReleaseSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.Null(info);
    }

    [Fact]
    public async Task TryGetLatestAsync_LiefertKeineAssetsUndKeinenChangelog()
    {
        var handler = new FakeHttpMessageHandler().When(@"pypi\.org/pypi/yt-dlp/json$", content: RealisticJson);
        using var client = new HttpClient(handler);
        var source = new PyPiReleaseSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.Empty(info!.Assets);
        Assert.Null(info.Changelog);
        Assert.Null(info.DownloadUrl);
    }
}
