using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services.Releases;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace MortysDLP.Tests.Releases;

/// <summary>
/// Prüft die Ausweichkette (<see cref="ResilientReleaseResolver"/>) über Attrappen-Quellen
/// (<see cref="FakeReleaseSource"/>) — unabhängig von HTTP, im Sekundenbereich. Für die
/// Kontingent-Prüfung kommen die echten GitHub-API-Quellen aus W2-T04a mit einem
/// <see cref="FakeHttpMessageHandler"/> zum Einsatz, der bei einem unerwarteten Aufruf wirft.
/// Siehe <c>werkstatt/tasks/W2-T04b.md</c>.
/// </summary>
public class ResilientReleaseResolverTests : IDisposable
{
    private readonly string _tempLogDir;

    public ResilientReleaseResolverTests()
    {
        _tempLogDir = Path.Combine(
            Path.GetTempPath(), "MortysDLP.Tests.ResilientReleaseResolver", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempLogDir);
        Log.LogsDirectory = _tempLogDir;
        Log.MinLevel = LogLevel.Debug;
        GitHubRateLimit.ResetForTests();
    }

    public void Dispose()
    {
        Log.CloseForTests();
        GitHubRateLimit.ResetForTests();
        try { Directory.Delete(_tempLogDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private static readonly ReleaseQuery Query = new("MortysTerminal", "MortysDLP");

    private static ReleaseInfo MakeInfo(string version) =>
        new(AppVersion.Parse(version), null, null, null, null, "test", []);

    [Fact]
    public async Task ResolveAsync_ErsteQuelleOhneErgebnis_ZweiteAntwortet()
    {
        var s1 = new FakeReleaseSource("s1");
        var s2 = new FakeReleaseSource("s2", isAuthoritative: true, result: MakeInfo("2026.06.01"));
        var resolver = new ResilientReleaseResolver([s1, s2]);

        var info = await resolver.ResolveAsync(Query, null, CancellationToken.None);

        Assert.Equal("2026.06.01", info!.Version.ToString());
        Assert.Equal(1, s1.CallCount);
        Assert.Equal(1, s2.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_ErsteZweiOhneErgebnis_DritteAntwortet()
    {
        var s1 = new FakeReleaseSource("s1");
        var s2 = new FakeReleaseSource("s2", throwException: new InvalidOperationException("boom"));
        var s3 = new FakeReleaseSource("s3", isAuthoritative: true, result: MakeInfo("2026.06.01"));
        var resolver = new ResilientReleaseResolver([s1, s2, s3]);

        var info = await resolver.ResolveAsync(Query, null, CancellationToken.None);

        Assert.Equal("2026.06.01", info!.Version.ToString());
        Assert.Equal(1, s1.CallCount);
        Assert.Equal(1, s2.CallCount);
        Assert.Equal(1, s3.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_HaengendeQuelle_WirdNachZeitlimitAbgebrochenUndBlockiertNicht()
    {
        var hanging = new FakeReleaseSource("hanging", delay: TimeSpan.FromSeconds(30));
        var s2 = new FakeReleaseSource("s2", isAuthoritative: true, result: MakeInfo("2026.06.01"));
        var resolver = new ResilientReleaseResolver([hanging, s2], perSourceTimeout: TimeSpan.FromMilliseconds(50));

        var info = await resolver.ResolveAsync(Query, null, CancellationToken.None);

        Assert.Equal("2026.06.01", info!.Version.ToString());
        Assert.Equal(1, hanging.CallCount);
        Assert.Equal(1, s2.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_AlleQuellenOhneErgebnis_LiefertNullUndProtokolliertJedeQuelle()
    {
        var s1 = new FakeReleaseSource("quelle-eins");
        var s2 = new FakeReleaseSource("quelle-zwei", throwException: new InvalidOperationException("boom"));
        var resolver = new ResilientReleaseResolver([s1, s2]);

        var info = await resolver.ResolveAsync(Query, null, CancellationToken.None);
        Log.CloseForTests();

        Assert.Null(info);
        string content = File.ReadAllText(Log.CurrentLogFile);
        Assert.Contains("quelle-eins", content);
        Assert.Contains("quelle-zwei", content);
    }

    [Fact]
    public async Task ResolveAsync_NichtPrimaereQuelleMitAlterVersion_FragtWeiter_NeuereGewinnt()
    {
        var current = AppVersion.Parse("2026.06.01");
        var stale = new FakeReleaseSource("stale", result: MakeInfo("2026.05.01"));
        var fresh = new FakeReleaseSource("fresh", result: MakeInfo("2026.07.01"));
        var resolver = new ResilientReleaseResolver([stale, fresh]);

        var info = await resolver.ResolveAsync(Query, current, CancellationToken.None);

        Assert.Equal("2026.07.01", info!.Version.ToString());
        Assert.Equal(1, stale.CallCount);
        Assert.Equal(1, fresh.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_AlleQuellenMitAlterVersion_LiefertHoechsteDavonNichtNull()
    {
        var current = AppVersion.Parse("2026.06.01");
        var stale1 = new FakeReleaseSource("stale1", result: MakeInfo("2026.03.01"));
        var stale2 = new FakeReleaseSource("stale2", result: MakeInfo("2026.05.01"));
        var resolver = new ResilientReleaseResolver([stale1, stale2]);

        var info = await resolver.ResolveAsync(Query, current, CancellationToken.None);

        Assert.NotNull(info);
        Assert.Equal("2026.05.01", info!.Version.ToString());
    }

    [Fact]
    public async Task ResolveAsync_PrimaereQuelleMitAlterVersion_BeendetKetteSofort()
    {
        var current = AppVersion.Parse("2026.06.01");
        var primary = new FakeReleaseSource("primary", isAuthoritative: true, result: MakeInfo("2026.06.01"));
        var never = new FakeReleaseSource("never", result: MakeInfo("2026.07.01"));
        var resolver = new ResilientReleaseResolver([primary, never]);

        var info = await resolver.ResolveAsync(Query, current, CancellationToken.None);

        Assert.Equal("2026.06.01", info!.Version.ToString());
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(0, never.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_CurrentIstNull_ErsteAntwortGewinntUnabhaengigVonAuthoritative()
    {
        var s1 = new FakeReleaseSource("s1", isAuthoritative: false, result: MakeInfo("2026.01.01"));
        var s2 = new FakeReleaseSource("s2", isAuthoritative: true, result: MakeInfo("2026.09.01"));
        var resolver = new ResilientReleaseResolver([s1, s2]);

        var info = await resolver.ResolveAsync(Query, null, CancellationToken.None);

        Assert.Equal("2026.01.01", info!.Version.ToString());
        Assert.Equal(1, s1.CallCount);
        Assert.Equal(0, s2.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_AbbruchDurchNutzer_WirftUndFragtNichtWeiter()
    {
        using var cts = new CancellationTokenSource();
        var s1 = new FakeReleaseSource("s1", delay: TimeSpan.FromSeconds(30));
        var s2 = new FakeReleaseSource("s2", result: MakeInfo("2026.06.01"));
        var resolver = new ResilientReleaseResolver([s1, s2]);

        cts.CancelAfter(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            resolver.ResolveAsync(Query, null, cts.Token));

        Assert.Equal(1, s1.CallCount);
        Assert.Equal(0, s2.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_ErschoepftesKontingent_ApiQuellenWerdenUebersprungen()
    {
        var headers = MakeExhaustedRateLimitHeaders();
        GitHubRateLimit.Observe(headers, DateTimeOffset.UtcNow);

        // Ein Handler ohne registrierte Muster wirft bei jedem Aufruf - so fällt sofort auf,
        // falls die Kette eine der beiden API-Quellen fälschlich doch befragt.
        using var client = new HttpClient(new FakeHttpMessageHandler());
        var apiLatest = new GitHubApiLatestSource(client);
        var apiList = new GitHubApiListSource(client);
        var fallback = new FakeReleaseSource("fallback", result: MakeInfo("2026.06.01"));
        var resolver = new ResilientReleaseResolver([apiLatest, apiList, fallback]);

        var info = await resolver.ResolveAsync(Query, null, CancellationToken.None);
        Log.CloseForTests();

        Assert.Equal("2026.06.01", info!.Version.ToString());
        Assert.Equal(1, fallback.CallCount);
        Assert.Contains("Kontingent", File.ReadAllText(Log.CurrentLogFile));
    }

    [Fact]
    public void CreateAppChain_LiefertVierQuellenInDerRichtigenReihenfolge()
    {
        var chain = ReleaseSources.CreateAppChain();

        Assert.Equal(4, chain.Count);
        Assert.IsType<GitHubApiLatestSource>(chain[0]);
        Assert.IsType<GitHubApiListSource>(chain[1]);
        Assert.IsType<GitHubAtomFeedSource>(chain[2]);
        Assert.IsType<GitHubRedirectSource>(chain[3]);
    }

    private static HttpResponseHeaders MakeExhaustedRateLimitHeaders()
    {
        var response = new HttpResponseMessage();
        response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0");
        response.Headers.TryAddWithoutValidation("X-RateLimit-Reset",
            DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        return response.Headers;
    }
}
