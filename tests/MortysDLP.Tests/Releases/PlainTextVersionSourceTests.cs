using MortysDLP.Services.Releases;
using System.Net;
using System.Net.Http;

namespace MortysDLP.Tests.Releases;

/// <summary>
/// Prüft <see cref="PlainTextVersionSource"/> — reine Logik über <c>ParsePlainText</c> und die
/// volle <c>TryGetLatestAsync</c>-Kette über <see cref="FakeHttpMessageHandler"/>. Kein Test
/// greift auf das echte Netz zu.
/// </summary>
public class PlainTextVersionSourceTests
{
    private const string PlainTextUrl = "https://www.gyan.dev/ffmpeg/builds/release-version";

    private static readonly ReleaseQuery Query = new("x", "y", PlainTextVersionUrl: PlainTextUrl);

    [Fact]
    public void ParsePlainText_ReineVersionsnummer_LiefertVersion()
    {
        var info = new PlainTextVersionSource("gyan-dev", isAuthoritative: false).ParsePlainText("7.1\n");

        Assert.NotNull(info);
        Assert.Equal("7.1", info!.Version.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html>keine Version</html>")]
    [InlineData("release-version: 7.1")]
    public void ParsePlainText_KeineVersionsnummer_LiefertNullStattStillschweigendemNull(string text)
    {
        // "kein stillschweigendes null" heißt hier: der Fall wird protokolliert (siehe
        // Log.Warn in ParsePlainText), nicht dass Assert eine Ausnahme statt null erwartet -
        // die öffentliche Vertragsantwort bleibt null, nur eben mit erklärender Zeile.
        Assert.Null(new PlainTextVersionSource("gyan-dev", isAuthoritative: false).ParsePlainText(text));
    }

    [Fact]
    public void IsAuthoritative_IstKonfigurierbar()
    {
        Assert.True(new PlainTextVersionSource("x", isAuthoritative: true).IsAuthoritative);
        Assert.False(new PlainTextVersionSource("x", isAuthoritative: false).IsAuthoritative);
    }

    [Fact]
    public void Name_UebernimmtDenUebergebenenNamen()
    {
        Assert.Equal("gyan-dev", new PlainTextVersionSource("gyan-dev", isAuthoritative: false).Name);
    }

    [Fact]
    public async Task TryGetLatestAsync_OhneUrl_LiefertNullOhneAnfrage()
    {
        var handler = new FakeHttpMessageHandler();
        using var client = new HttpClient(handler);
        var source = new PlainTextVersionSource("gyan-dev", isAuthoritative: false, client);

        var info = await source.TryGetLatestAsync(new ReleaseQuery("x", "y"), CancellationToken.None);

        Assert.Null(info);
        Assert.Equal(0, handler.TotalCallCount);
    }

    [Fact]
    public async Task TryGetLatestAsync_NichtZugelassenerHost_LiefertNullOhneAnfrage()
    {
        var handler = new FakeHttpMessageHandler();
        using var client = new HttpClient(handler);
        var source = new PlainTextVersionSource("evil", isAuthoritative: false, client);
        var query = new ReleaseQuery("x", "y", PlainTextVersionUrl: "https://evil.example/version.txt");

        var info = await source.TryGetLatestAsync(query, CancellationToken.None);

        Assert.Null(info);
        Assert.Equal(0, handler.TotalCallCount);
    }

    [Fact]
    public async Task TryGetLatestAsync_ErfolgreicheAntwort_LiestVersion()
    {
        var handler = new FakeHttpMessageHandler().When(@"gyan\.dev/ffmpeg/builds/release-version$", content: "7.1");
        using var client = new HttpClient(handler);
        var source = new PlainTextVersionSource("gyan-dev", isAuthoritative: false, client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.NotNull(info);
        Assert.Equal("7.1", info!.Version.ToString());
        Assert.Equal("gyan-dev", info.SourceName);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task TryGetLatestAsync_FehlerStatus_LiefertNullOhneWurf(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler().When(@"gyan\.dev/ffmpeg/builds/release-version$", status: status);
        using var client = new HttpClient(handler);
        var source = new PlainTextVersionSource("gyan-dev", isAuthoritative: false, client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.Null(info);
    }
}
