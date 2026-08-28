using MortysDLP.Services.Releases;
using System.Net;
using System.Net.Http;

namespace MortysDLP.Tests.Releases;

/// <summary>
/// Prüft die von Hand gepflegte <c>version.json</c>-Quelle — reine Logik über
/// <c>ParseVersionJson</c> und der volle Weg über <see cref="FakeHttpMessageHandler"/>, ohne
/// echten Netzzugriff. Siehe <c>werkstatt/tasks/W2-T05.md</c>.
/// </summary>
public class VersionJsonSourceTests
{
    private const string GueltigeDatei = """
        {
          "schemaVersion": 1,
          "stable": {
            "version": "2026.06.01",
            "url": "https://github.com/MortysTerminal/MortysDLP/releases/download/2026.06.01/MortysDLP.zip",
            "sha256": "db1a7003543650836fd5f54bc55c0d04209fc5ce618fb791b5d1e4a045e9bdd6",
            "size": 381505,
            "requiresDotnet": "10.0"
          }
        }
        """;

    private static readonly ReleaseQuery Query = new("MortysTerminal", "MortysDLP");

    [Fact]
    public void ParseVersionJson_GueltigeDatei_LiefertVersionUrlGroesseUndPruefsumme()
    {
        var info = new VersionJsonReleaseSource().ParseVersionJson(GueltigeDatei);

        Assert.NotNull(info);
        Assert.Equal("2026.06.01", info!.Version.ToString());
        Assert.Equal(
            "https://github.com/MortysTerminal/MortysDLP/releases/download/2026.06.01/MortysDLP.zip",
            info.DownloadUrl);
        Assert.Equal(381505, info.ExpectedSize);
        Assert.Equal("db1a7003543650836fd5f54bc55c0d04209fc5ce618fb791b5d1e4a045e9bdd6", info.Sha256);
        Assert.Null(info.Changelog);
    }

    [Fact]
    public void IsAuthoritative_IstFalse()
    {
        // Von Hand gepflegt - kann als einzige Quelle dauerhaft falsch sein, wenn die Pflege
        // beim Release vergessen wird. Darf die Kette deshalb nie abschließend beenden.
        Assert.False(new VersionJsonReleaseSource().IsAuthoritative);
    }

    [Theory]
    [InlineData("""{"schemaVersion":2,"stable":{"version":"2026.06.01","url":"https://github.com/x/y"}}""")] // falsche schemaVersion
    [InlineData("""{"schemaVersion":1,"stable":{"version":"nightly","url":"https://github.com/x/y"}}""")] // unlesbare Version
    [InlineData("""{"schemaVersion":1,"stable":{"version":"2026.06.01","url":"http://github.com/x/y"}}""")] // http:// statt https://
    [InlineData("""{"schemaVersion":1,"stable":{"version":"2026.06.01","url":"https://evil.com/x/y"}}""")] // fremder Host
    [InlineData("""{"schemaVersion":1,"stable":{"version":"2026.06.01","url":"https://github.com/x/y","sha256":"abc"}}""")] // Pruefsumme zu kurz
    [InlineData("""{"schemaVersion":1,"stable":{"version":"2026.06.01","url":"https://github.com/x/y","size":0}}""")] // Groesse nicht positiv
    [InlineData("""{"schemaVersion":1,"dev":{"version":"2026.06.01","url":"https://github.com/x/y"}}""")] // fehlender stable-Abschnitt
    [InlineData("""{"schemaVersion":1,"stable":{"url":"https://github.com/x/y"}}""")] // fehlende Version
    [InlineData("""{"schemaVersion":1,"stable":{"version":"2026.06.01"}}""")] // fehlende URL
    [InlineData("{kaputtes json")]
    [InlineData("")]
    public void ParseVersionJson_FehlerhafteDatei_LiefertNullOhneWurf(string json)
    {
        Assert.Null(new VersionJsonReleaseSource().ParseVersionJson(json));
    }

    [Fact]
    public void ParseVersionJson_PruefsummeMit63Zeichen_LiefertNull()
    {
        string kurzeSha = new string('a', 63);
        string json = $$"""
            {
              "schemaVersion": 1,
              "stable": {
                "version": "2026.06.01",
                "url": "https://github.com/x/y",
                "sha256": "{{kurzeSha}}"
              }
            }
            """;

        Assert.Null(new VersionJsonReleaseSource().ParseVersionJson(json));
    }

    [Fact]
    public void ParseVersionJson_OhneSha256UndSize_IstTrotzdemGueltig()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "stable": { "version": "2026.06.01", "url": "https://github.com/x/y" }
            }
            """;

        var info = new VersionJsonReleaseSource().ParseVersionJson(json);

        Assert.NotNull(info);
        Assert.Null(info!.Sha256);
        Assert.Null(info.ExpectedSize);
    }

    [Fact]
    public async Task TryGetLatestAsync_GueltigeAntwort_LiefertReleaseInfo()
    {
        var handler = new FakeHttpMessageHandler().When(@"version\.json$", content: GueltigeDatei);
        using var client = new HttpClient(handler);
        var source = new VersionJsonReleaseSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.NotNull(info);
        Assert.Equal("2026.06.01", info!.Version.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task TryGetLatestAsync_FehlerStatus_LiefertNullOhneWurf(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler().When(@"version\.json$", status: status);
        using var client = new HttpClient(handler);
        var source = new VersionJsonReleaseSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.Null(info);
    }

    [Fact]
    public async Task ResolveAsync_InEinerKetteMitVierVorherigenQuellen_VersionJsonNurAlsLetzteBefragt()
    {
        var s1 = new FakeReleaseSource("s1");
        var s2 = new FakeReleaseSource("s2");
        var s3 = new FakeReleaseSource("s3");
        var s4 = new FakeReleaseSource("s4");

        var handler = new FakeHttpMessageHandler().When(@"version\.json$", content: GueltigeDatei);
        using var client = new HttpClient(handler);
        var versionJson = new VersionJsonReleaseSource(client);

        var resolver = new ResilientReleaseResolver([s1, s2, s3, s4, versionJson]);

        var info = await resolver.ResolveAsync(Query, null, CancellationToken.None);

        Assert.NotNull(info);
        Assert.Equal("2026.06.01", info!.Version.ToString());
        Assert.Equal(1, s1.CallCount);
        Assert.Equal(1, s2.CallCount);
        Assert.Equal(1, s3.CallCount);
        Assert.Equal(1, s4.CallCount);
        Assert.Equal(1, handler.TotalCallCount);
    }
}
