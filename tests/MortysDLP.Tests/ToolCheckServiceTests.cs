using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services;
using MortysDLP.Services.Releases;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="ToolCheckService"/> über eine Attrappe des Resolvers und einen echten
/// <see cref="UpdateCache"/> gegen ein Temp-Verzeichnis — mit übergebener Zeit, ohne echtes
/// Warten. Die Fälle entsprechen den früheren <c>UpdateCheckServiceTests</c> (App als
/// Schlüssel <c>"app"</c>, 6-Stunden-Laufzeit), ergänzt um Fälle, die erst durch die
/// Verallgemeinerung möglich wurden (eigener Schlüssel, eigene Laufzeit, unbekannte laufende
/// Version).
/// </summary>
public class ToolCheckServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-08-27T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly ReleaseQuery Query = new("MortysTerminal", "MortysDLP");

    private readonly string _tempDir;
    private readonly string _cacheFilePath;

    public ToolCheckServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.ToolCheckService", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _cacheFilePath = Path.Combine(_tempDir, "update-cache.json");
        GitHubRateLimit.ResetForTests();
    }

    public void Dispose()
    {
        GitHubRateLimit.ResetForTests();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private UpdateCache MakeCache() => new(_cacheFilePath);

    private static ReleaseInfo MakeInfo(string version, string source = "github-api-latest") =>
        new(AppVersion.Parse(version), "https://example.invalid/x.zip", "Changelog", null, null, source, []);

    [Fact]
    public async Task CheckAsync_FrischerCacheEintrag_ResolverWirdNichtAufgerufen()
    {
        var cache = MakeCache();
        await cache.WriteAsync("app", new UpdateCacheEntry
        {
            CheckedUtc = Now - TimeSpan.FromHours(1),
            Version = "2026.07.01",
            DownloadUrl = "https://example.invalid/x.zip",
            Source = "github-api-latest",
        }, CancellationToken.None);

        var resolver = new FakeReleaseResolver(MakeInfo("2026.09.01"));
        var service = new ToolCheckService(resolver, cache, () => Now);

        var result = await service.CheckAsync(
            "app", Query, AppVersion.Parse("2026.06.01"), ToolCheckService.AppCacheLifetime, force: false, CancellationToken.None);

        Assert.Equal(0, resolver.CallCount);
        Assert.True(result.FromCache);
        Assert.Equal("2026.07.01", result.Info!.Version.ToString());
        Assert.True(result.UpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_AbgelaufenerEintrag_ResolverWirdAufgerufenUndCacheAktualisiert()
    {
        var cache = MakeCache();
        await cache.WriteAsync("app", new UpdateCacheEntry
        {
            CheckedUtc = Now - TimeSpan.FromHours(7),
            Version = "2026.06.01",
            Source = "github-api-latest",
        }, CancellationToken.None);

        var resolver = new FakeReleaseResolver(MakeInfo("2026.09.01"));
        var service = new ToolCheckService(resolver, cache, () => Now);

        var result = await service.CheckAsync(
            "app", Query, AppVersion.Parse("2026.06.01"), ToolCheckService.AppCacheLifetime, force: false, CancellationToken.None);

        Assert.Equal(1, resolver.CallCount);
        Assert.False(result.FromCache);
        Assert.Equal("2026.09.01", result.Info!.Version.ToString());

        var updated = await cache.ReadAsync("app", CancellationToken.None);
        Assert.Equal("2026.09.01", updated!.Version);
        Assert.Equal(Now, updated.CheckedUtc);
    }

    [Fact]
    public async Task CheckAsync_ForceTrue_ResolverWirdAuchBeiFrischemEintragAufgerufen()
    {
        var cache = MakeCache();
        await cache.WriteAsync("app", new UpdateCacheEntry
        {
            CheckedUtc = Now - TimeSpan.FromMinutes(5),
            Version = "2026.06.01",
            Source = "github-api-latest",
        }, CancellationToken.None);

        var resolver = new FakeReleaseResolver(MakeInfo("2026.09.01"));
        var service = new ToolCheckService(resolver, cache, () => Now);

        var result = await service.CheckAsync(
            "app", Query, AppVersion.Parse("2026.06.01"), ToolCheckService.AppCacheLifetime, force: true, CancellationToken.None);

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal("2026.09.01", result.Info!.Version.ToString());
    }

    [Fact]
    public async Task CheckAsync_ResolverLiefertNull_AbgelaufenerCache_AlterWertWirdVerwendet()
    {
        var cache = MakeCache();
        await cache.WriteAsync("app", new UpdateCacheEntry
        {
            CheckedUtc = Now - TimeSpan.FromHours(7),
            Version = "2026.06.01",
            DownloadUrl = "https://example.invalid/alt.zip",
            Source = "github-api-latest",
        }, CancellationToken.None);

        var resolver = new FakeReleaseResolver(result: null);
        var service = new ToolCheckService(resolver, cache, () => Now);

        var result = await service.CheckAsync(
            "app", Query, AppVersion.Parse("2026.06.01"), ToolCheckService.AppCacheLifetime, force: false, CancellationToken.None);

        Assert.True(result.FromCache);
        Assert.Equal("2026.06.01", result.Info!.Version.ToString());
    }

    [Fact]
    public async Task CheckAsync_ResolverLiefertNull_KeinCache_LiefertNull()
    {
        var cache = MakeCache();
        var resolver = new FakeReleaseResolver(result: null);
        var service = new ToolCheckService(resolver, cache, () => Now);

        var result = await service.CheckAsync(
            "app", Query, AppVersion.Parse("2026.06.01"), ToolCheckService.AppCacheLifetime, force: false, CancellationToken.None);

        Assert.Null(result.Info);
        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_DefekteCacheDatei_WieKeinCache_KeineAusnahme()
    {
        await File.WriteAllTextAsync(_cacheFilePath, "{kaputtes json");
        var cache = MakeCache();
        var resolver = new FakeReleaseResolver(MakeInfo("2026.09.01"));
        var service = new ToolCheckService(resolver, cache, () => Now);

        var exception = await Record.ExceptionAsync(() => service.CheckAsync(
            "app", Query, AppVersion.Parse("2026.06.01"), ToolCheckService.AppCacheLifetime, force: false, CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal(1, resolver.CallCount);
    }

    [Fact]
    public async Task CheckAsync_GemeldeteVersionGleichOderAelter_UpdateAvailableIstFalse()
    {
        var cache = MakeCache();
        var resolver = new FakeReleaseResolver(MakeInfo("2026.06.01"));
        var service = new ToolCheckService(resolver, cache, () => Now);

        var result = await service.CheckAsync(
            "app", Query, AppVersion.Parse("2026.06.01"), ToolCheckService.AppCacheLifetime, force: false, CancellationToken.None);

        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_ZeitstempelAusDerZukunft_GiltAlsAbgelaufen_ResolverWirdAufgerufen()
    {
        var cache = MakeCache();
        await cache.WriteAsync("app", new UpdateCacheEntry
        {
            CheckedUtc = Now + TimeSpan.FromHours(1),
            Version = "2026.06.01",
            Source = "github-api-latest",
        }, CancellationToken.None);

        var resolver = new FakeReleaseResolver(MakeInfo("2026.09.01"));
        var service = new ToolCheckService(resolver, cache, () => Now);

        var result = await service.CheckAsync(
            "app", Query, AppVersion.Parse("2026.06.01"), ToolCheckService.AppCacheLifetime, force: false, CancellationToken.None);

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal("2026.09.01", result.Info!.Version.ToString());
    }

    [Fact]
    public async Task CheckAsync_LaufendeVersionUnbekannt_JedeGefundeneVersionGiltAlsVerfuegbar()
    {
        // Anders als bei der App (dort wird das vom Aufrufer vorher abgefangen) ist "Werkzeug
        // noch nicht installiert" für ein Werkzeug der Normalfall, kein Fehler - "nicht neuer"
        // lässt sich ohne Vergleichswert nicht behaupten.
        var cache = MakeCache();
        var resolver = new FakeReleaseResolver(MakeInfo("2026.08.19"));
        var service = new ToolCheckService(resolver, cache, () => Now);

        var result = await service.CheckAsync(
            "yt-dlp", Query, currentVersion: null, ToolCheckService.ToolCacheLifetime, force: false, CancellationToken.None);

        Assert.True(result.UpdateAvailable);
        Assert.Equal("2026.08.19", result.Info!.Version.ToString());
    }

    [Fact]
    public async Task CheckAsync_304_CacheBleibtGueltigUndLaufzeitBeginntNeu()
    {
        var cache = MakeCache();
        await cache.WriteAsync("app", new UpdateCacheEntry
        {
            CheckedUtc = Now - TimeSpan.FromHours(7),
            Version = "2026.07.01",
            DownloadUrl = "https://example.invalid/x.zip",
            ETag = "\"abc123\"",
            Source = "github-api-latest",
        }, CancellationToken.None);

        var notModified = MakeInfo("0") with { NotModified = true, ETag = "\"abc123\"" };
        var resolver = new FakeReleaseResolver(notModified);
        var service = new ToolCheckService(resolver, cache, () => Now);

        var result = await service.CheckAsync(
            "app", Query, AppVersion.Parse("2026.06.01"), ToolCheckService.AppCacheLifetime, force: false, CancellationToken.None);

        Assert.True(result.FromCache);
        Assert.Equal("2026.07.01", result.Info!.Version.ToString());

        var updated = await cache.ReadAsync("app", CancellationToken.None);
        Assert.Equal(Now, updated!.CheckedUtc);
        Assert.Equal("2026.07.01", updated.Version);
    }

    [Fact]
    public async Task CheckAsync_ZweiVerschiedeneSchluessel_TeilenSichDenCacheNicht()
    {
        var cache = MakeCache();
        await cache.WriteAsync("app", new UpdateCacheEntry
        {
            CheckedUtc = Now - TimeSpan.FromHours(1),
            Version = "2026.06.01",
            Source = "github-api-latest",
        }, CancellationToken.None);

        var resolver = new FakeReleaseResolver(MakeInfo("2026.08.19"));
        var service = new ToolCheckService(resolver, cache, () => Now);

        // "yt-dlp" hat keinen eigenen Eintrag - muss den Resolver befragen, obwohl "app"
        // gerade frisch im Cache steht.
        var result = await service.CheckAsync(
            "yt-dlp", Query, currentVersion: null, ToolCheckService.ToolCacheLifetime, force: false, CancellationToken.None);

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal("2026.08.19", result.Info!.Version.ToString());
    }

    [Fact]
    public async Task CheckAsync_ErgebnisTraegtSha256UndExpectedSizeInDenCache()
    {
        var cache = MakeCache();
        var info = MakeInfo("2026.06.01", "version-json") with { Sha256 = new string('a', 64), ExpectedSize = 4242 };
        var resolver = new FakeReleaseResolver(info);
        var service = new ToolCheckService(resolver, cache, () => Now);

        await service.CheckAsync(
            "yt-dlp", Query, currentVersion: null, ToolCheckService.ToolCacheLifetime, force: false, CancellationToken.None);

        var cached = await cache.ReadAsync("yt-dlp", CancellationToken.None);
        Assert.Equal(info.Sha256, cached!.Sha256);
        Assert.Equal(info.ExpectedSize, cached.ExpectedSize);
    }

    [Fact]
    public async Task CheckAsync_WertetGetterZeitFuerJedenAufrufNeuAus()
    {
        // now() wird bei jedem Aufruf neu ausgewertet statt einmal im Konstruktor eingefroren -
        // wichtig, damit ein länger laufender Prozess nicht mit der Startzeit rechnet.
        var cache = MakeCache();
        var resolver = new FakeReleaseResolver(MakeInfo("2026.06.01"));
        var calls = 0;
        var service = new ToolCheckService(resolver, cache, () => { calls++; return Now; });

        await service.CheckAsync(
            "app", Query, AppVersion.Parse("2026.06.01"), ToolCheckService.AppCacheLifetime, force: false, CancellationToken.None);

        Assert.True(calls > 0);
    }

    private sealed class FakeReleaseResolver(ReleaseInfo? result = null) : IReleaseResolver
    {
        public int CallCount { get; private set; }

        public Task<ReleaseInfo?> ResolveAsync(ReleaseQuery query, AppVersion? current, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }
}
