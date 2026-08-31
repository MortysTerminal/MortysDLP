using MortysDLP.Services.Releases;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace MortysDLP.Tests.Releases;

/// <summary>
/// Prüft die vier unabhängigen Release-Quellen — reine Logik über die internen Parse-Methoden
/// und die volle <c>TryGetLatestAsync</c>-Kette über <see cref="FakeHttpMessageHandler"/>. Kein
/// Test greift auf das echte Netz zu.
/// </summary>
public class ReleaseSourceTests
{
    private const string LatestJson = """
        {
          "tag_name": "2026.06.01",
          "body": "Changelog-Text",
          "assets": [
            {
              "name": "MortysDLP.zip",
              "browser_download_url": "https://github.com/MortysTerminal/MortysDLP/releases/download/2026.06.01/MortysDLP.zip",
              "size": 390656
            }
          ]
        }
        """;

    private const string ListJson = """
        [
          { "tag_name": "2026.05.01", "draft": false, "prerelease": false },
          { "tag_name": "2026.07.01", "draft": true,  "prerelease": false },
          { "tag_name": "2026.09.01-dev.1", "draft": false, "prerelease": true },
          { "tag_name": "2026.06.01", "draft": false, "prerelease": false }
        ]
        """;

    private const string AtomFeedIdVorTitel = """
        <?xml version="1.0" encoding="utf-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom">
          <entry>
            <id>tag:github.com,2008:Repository/123456789/2026.06.01</id>
            <title>Ein toller Release</title>
            <content type="html">Changelog-Text</content>
          </entry>
        </feed>
        """;

    private const string AtomFeedIdUnlesbar = """
        <?xml version="1.0" encoding="utf-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom">
          <entry>
            <id>tag:github.com,2008:Repository/123456789/nightly-build</id>
            <title>2026.06.02</title>
          </entry>
        </feed>
        """;

    private const string AtomFeedBeidesUnlesbar = """
        <?xml version="1.0" encoding="utf-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom">
          <entry>
            <id>tag:github.com,2008:Repository/123456789/nightly</id>
            <title>Ein Release ohne Versionsnummer im Titel</title>
          </entry>
        </feed>
        """;

    private static readonly ReleaseQuery Query = new("MortysTerminal", "MortysDLP");

    // --- GitHubApiLatestSource -------------------------------------------------------------

    [Fact]
    public void GitHubApiLatestSource_RealistischeAntwort_LiefertKorrekteVersion()
    {
        var info = new GitHubApiLatestSource().ParseRelease(LatestJson);

        Assert.NotNull(info);
        Assert.Equal("2026.06.01", info!.Version.ToString());
        Assert.Equal("Changelog-Text", info.Changelog);
        Assert.Single(info.Assets);
        Assert.Equal("MortysDLP.zip", info.Assets[0].Name);
    }

    [Fact]
    public void GitHubApiLatestSource_IstAuthoritative()
    {
        Assert.True(new GitHubApiLatestSource().IsAuthoritative);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{nicht valides json")]
    [InlineData("{}")]
    [InlineData("""{"tag_name":"nightly"}""")]
    public void GitHubApiLatestSource_UnbrauchbarerInhalt_LiefertNull(string json)
    {
        Assert.Null(new GitHubApiLatestSource().ParseRelease(json));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GitHubApiLatestSource_FehlerStatus_LiefertNullOhneWurf(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler().When(@"releases/latest$", status: status);
        using var client = new HttpClient(handler);
        var source = new GitHubApiLatestSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.Null(info);
    }

    [Fact]
    public async Task GitHubApiLatestSource_304_LiefertNotModifiedMitEcho()
    {
        var handler = new FakeHttpMessageHandler().When(@"releases/latest$", status: HttpStatusCode.NotModified);
        using var client = new HttpClient(handler);
        var source = new GitHubApiLatestSource(client);

        var info = await source.TryGetLatestAsync(Query with { ETag = "\"abc123\"" }, CancellationToken.None);

        Assert.NotNull(info);
        Assert.True(info!.NotModified);
        Assert.Equal("\"abc123\"", info.ETag);
    }

    [Fact]
    public async Task GitHubApiLatestSource_ErfolgreicheAntwort_UebernimmtETagAusKopfzeile()
    {
        var headers = new Dictionary<string, string> { ["ETag"] = "\"neu456\"" };
        var handler = new FakeHttpMessageHandler().When(@"releases/latest$", content: LatestJson, headers: headers);
        using var client = new HttpClient(handler);
        var source = new GitHubApiLatestSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.NotNull(info);
        Assert.False(info!.NotModified);
        Assert.Equal("\"neu456\"", info.ETag);
    }

    // --- GitHubApiListSource -----------------------------------------------------------------

    [Fact]
    public void GitHubApiListSource_EntwuerfeUndVorabversionenGefiltert_WaehltHoechsteVerbleibendeVersion()
    {
        var info = new GitHubApiListSource().ParseHighestRelease(ListJson, Query);

        Assert.NotNull(info);
        Assert.Equal("2026.06.01", info!.Version.ToString());
    }

    [Fact]
    public void GitHubApiListSource_IstAuthoritative()
    {
        Assert.True(new GitHubApiListSource().IsAuthoritative);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[nicht valides json")]
    [InlineData("{}")]
    [InlineData("""[{"tag_name":"nightly"}]""")]
    public void GitHubApiListSource_UnbrauchbarerInhalt_LiefertNull(string json)
    {
        Assert.Null(new GitHubApiListSource().ParseHighestRelease(json, Query));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GitHubApiListSource_FehlerStatus_LiefertNullOhneWurf(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler().When(@"releases\?per_page=10$", status: status);
        using var client = new HttpClient(handler);
        var source = new GitHubApiListSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.Null(info);
    }

    [Fact]
    public async Task GitHubApiListSource_304_LiefertNotModifiedMitEcho()
    {
        var handler = new FakeHttpMessageHandler().When(@"releases\?per_page=10$", status: HttpStatusCode.NotModified);
        using var client = new HttpClient(handler);
        var source = new GitHubApiListSource(client);

        var info = await source.TryGetLatestAsync(Query with { ETag = "\"abc123\"" }, CancellationToken.None);

        Assert.NotNull(info);
        Assert.True(info!.NotModified);
        Assert.Equal("\"abc123\"", info.ETag);
    }

    [Fact]
    public async Task GitHubApiListSource_ErfolgreicheAntwort_UebernimmtETagAusKopfzeile()
    {
        var headers = new Dictionary<string, string> { ["ETag"] = "\"neu456\"" };
        var handler = new FakeHttpMessageHandler().When(@"releases\?per_page=10$", content: ListJson, headers: headers);
        using var client = new HttpClient(handler);
        var source = new GitHubApiListSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.NotNull(info);
        Assert.False(info!.NotModified);
        Assert.Equal("\"neu456\"", info.ETag);
    }

    // --- GitHubAtomFeedSource ----------------------------------------------------------------

    [Fact]
    public void GitHubAtomFeedSource_BevorzugtIdGegenueberTitle()
    {
        var info = new GitHubAtomFeedSource().ParseFeed(AtomFeedIdVorTitel, Query);

        Assert.NotNull(info);
        Assert.Equal("2026.06.01", info!.Version.ToString());
        Assert.Equal("Changelog-Text", info.Changelog);
    }

    [Fact]
    public void GitHubAtomFeedSource_IdUnlesbar_WeichtAufTitleAus()
    {
        var info = new GitHubAtomFeedSource().ParseFeed(AtomFeedIdUnlesbar, Query);

        Assert.NotNull(info);
        Assert.Equal("2026.06.02", info!.Version.ToString());
    }

    [Fact]
    public void GitHubAtomFeedSource_IdUndTitleUnlesbar_LiefertNull()
    {
        Assert.Null(new GitHubAtomFeedSource().ParseFeed(AtomFeedBeidesUnlesbar, Query));
    }

    [Fact]
    public void GitHubAtomFeedSource_IstNichtAuthoritative()
    {
        Assert.False(new GitHubAtomFeedSource().IsAuthoritative);
    }

    [Theory]
    [InlineData("")]
    [InlineData("kein xml")]
    public void GitHubAtomFeedSource_KaputterInhalt_LiefertNull(string xml)
    {
        Assert.Null(new GitHubAtomFeedSource().ParseFeed(xml, Query));
    }

    [Fact]
    public void GitHubAtomFeedSource_MitDownloadUrlTemplate_LoestUrlAuf()
    {
        var query = Query with
        {
            DownloadUrlTemplate = "https://github.com/{owner}/{repo}/releases/download/{tag}/MortysDLP.zip",
        };

        var info = new GitHubAtomFeedSource().ParseFeed(AtomFeedIdVorTitel, query);

        Assert.Equal(
            "https://github.com/MortysTerminal/MortysDLP/releases/download/2026.06.01/MortysDLP.zip",
            info!.DownloadUrl);
    }

    [Fact]
    public void GitHubAtomFeedSource_OhneDownloadUrlTemplate_DownloadUrlIstNull()
    {
        var info = new GitHubAtomFeedSource().ParseFeed(AtomFeedIdVorTitel, Query);

        Assert.Null(info!.DownloadUrl);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GitHubAtomFeedSource_FehlerStatus_LiefertNullOhneWurf(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler().When(@"releases\.atom$", status: status);
        using var client = new HttpClient(handler);
        var source = new GitHubAtomFeedSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.Null(info);
    }

    // --- GitHubRedirectSource ----------------------------------------------------------------

    [Fact]
    public void GitHubRedirectSource_MitWeiterleitung_LiestTagAusLocationHeader()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri("https://github.com/MortysTerminal/MortysDLP/releases/tag/v2026.09.01");

        var info = new GitHubRedirectSource().ParseRedirect(response, Query);

        Assert.NotNull(info);
        Assert.Equal("2026.09.01", info!.Version.ToString());
    }

    [Fact]
    public void GitHubRedirectSource_OhneWeiterleitung_LiefertNull()
    {
        // Ein Client, der die Weiterleitung bereits selbst aufgelöst hat, liefert die
        // Endantwort ohne Location-Kopfzeile - genau der Fall, den Http.NoRedirect verhindert.
        using var response = new HttpResponseMessage(HttpStatusCode.OK);

        var info = new GitHubRedirectSource().ParseRedirect(response, Query);

        Assert.Null(info);
    }

    [Fact]
    public void GitHubRedirectSource_UnlesbarerTag_LiefertNull()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri("https://github.com/MortysTerminal/MortysDLP/releases/tag/nightly");

        var info = new GitHubRedirectSource().ParseRedirect(response, Query);

        Assert.Null(info);
    }

    [Fact]
    public void GitHubRedirectSource_IstNichtAuthoritative()
    {
        Assert.False(new GitHubRedirectSource().IsAuthoritative);
    }

    [Fact]
    public void GitHubRedirectSource_MitDownloadUrlTemplate_LoestRohformDesTagsAuf()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri("https://github.com/MortysTerminal/MortysDLP/releases/tag/v2026.09.01");
        var query = Query with
        {
            DownloadUrlTemplate = "https://github.com/{owner}/{repo}/releases/download/{tag}/MortysDLP.zip",
        };

        var info = new GitHubRedirectSource().ParseRedirect(response, query);

        Assert.Equal(
            "https://github.com/MortysTerminal/MortysDLP/releases/download/v2026.09.01/MortysDLP.zip",
            info!.DownloadUrl);
    }

    [Fact]
    public void GitHubRedirectSource_OhneDownloadUrlTemplate_DownloadUrlIstNull()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri("https://github.com/MortysTerminal/MortysDLP/releases/tag/v2026.09.01");

        var info = new GitHubRedirectSource().ParseRedirect(response, Query);

        Assert.Null(info!.DownloadUrl);
    }

    [Fact]
    public async Task GitHubRedirectSource_TryGetLatestAsync_MitWeiterleitung_LiefertVersion()
    {
        var headers = new Dictionary<string, string>
        {
            ["Location"] = "https://github.com/MortysTerminal/MortysDLP/releases/tag/v2026.09.01",
        };
        var handler = new FakeHttpMessageHandler().When(
            @"releases/latest$", status: HttpStatusCode.Found, headers: headers);
        using var client = new HttpClient(handler);
        var source = new GitHubRedirectSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.NotNull(info);
        Assert.Equal("2026.09.01", info!.Version.ToString());
    }

    [Fact]
    public async Task GitHubRedirectSource_TryGetLatestAsync_FolgenderClientOhneLocation_LiefertNull()
    {
        var handler = new FakeHttpMessageHandler().When(@"releases/latest$", status: HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var source = new GitHubRedirectSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.Null(info);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GitHubRedirectSource_FehlerStatus_LiefertNullOhneWurf(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler().When(@"releases/latest$", status: status);
        using var client = new HttpClient(handler);
        var source = new GitHubRedirectSource(client);

        var info = await source.TryGetLatestAsync(Query, CancellationToken.None);

        Assert.Null(info);
    }

    // --- Übergreifend: Normalisierung der Anzeige ---------------------------------------------

    [Fact]
    public void ReleaseInfo_DesselbenReleases_AusVerschiedenenQuellen_LiefertDenselbenAnzeigetext()
    {
        var apiInfo = new GitHubApiLatestSource().ParseRelease("""{"tag_name":"2026.09.01"}""");

        using var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri("https://github.com/MortysTerminal/MortysDLP/releases/tag/v2026.09.01");
        var redirectInfo = new GitHubRedirectSource().ParseRedirect(response, Query);

        Assert.NotNull(apiInfo);
        Assert.NotNull(redirectInfo);
        Assert.Equal(apiInfo!.Version.ToString(), redirectInfo!.Version.ToString());
    }
}
