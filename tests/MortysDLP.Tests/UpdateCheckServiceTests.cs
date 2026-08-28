using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services;
using MortysDLP.Services.Releases;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="UpdateCheckService"/> über eine Attrappe des Resolvers und einen echten
/// <see cref="UpdateCache"/> gegen ein Temp-Verzeichnis — mit übergebener Zeit, ohne echtes
/// Warten. Siehe <c>werkstatt/tasks/W2-T06.md</c>.
/// </summary>
public class UpdateCheckServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-08-27T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private readonly string _tempDir;
    private readonly string _cacheFilePath;

    public UpdateCheckServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.UpdateCheckService", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _cacheFilePath = Path.Combine(_tempDir, "update-cache.json");
        AppInfo.CurrentVersion = AppVersion.Parse("2026.06.01");
    }

    public void Dispose()
    {
        AppInfo.ResetForTests();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private UpdateCache MakeCache() => new(_cacheFilePath);

    private static ReleaseInfo MakeInfo(string version, string source = "github-api-latest") =>
        new(AppVersion.Parse(version), "https://example.invalid/x.zip", "Changelog", null, null, source, []);

    [Fact]
    public async Task CheckAppAsync_FrischerCacheEintrag_ResolverWirdNichtAufgerufen()
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
        var service = new UpdateCheckService(resolver, cache, () => Now);

        var result = await service.CheckAppAsync(force: false, CancellationToken.None);

        Assert.Equal(0, resolver.CallCount);
        Assert.True(result.FromCache);
        Assert.Equal("2026.07.01", result.Info!.Version.ToString());
        Assert.True(result.UpdateAvailable);
    }

    [Fact]
    public async Task CheckAppAsync_AbgelaufenerEintrag_ResolverWirdAufgerufenUndCacheAktualisiert()
    {
        var cache = MakeCache();
        await cache.WriteAsync("app", new UpdateCacheEntry
        {
            CheckedUtc = Now - TimeSpan.FromHours(7),
            Version = "2026.06.01",
            Source = "github-api-latest",
        }, CancellationToken.None);

        var resolver = new FakeReleaseResolver(MakeInfo("2026.09.01"));
        var service = new UpdateCheckService(resolver, cache, () => Now);

        var result = await service.CheckAppAsync(force: false, CancellationToken.None);

        Assert.Equal(1, resolver.CallCount);
        Assert.False(result.FromCache);
        Assert.Equal("2026.09.01", result.Info!.Version.ToString());

        var updated = await cache.ReadAsync("app", CancellationToken.None);
        Assert.Equal("2026.09.01", updated!.Version);
        Assert.Equal(Now, updated.CheckedUtc);
    }

    [Fact]
    public async Task CheckAppAsync_ForceTrue_ResolverWirdAuchBeiFrischemEintragAufgerufen()
    {
        var cache = MakeCache();
        await cache.WriteAsync("app", new UpdateCacheEntry
        {
            CheckedUtc = Now - TimeSpan.FromMinutes(5),
            Version = "2026.06.01",
            Source = "github-api-latest",
        }, CancellationToken.None);

        var resolver = new FakeReleaseResolver(MakeInfo("2026.09.01"));
        var service = new UpdateCheckService(resolver, cache, () => Now);

        var result = await service.CheckAppAsync(force: true, CancellationToken.None);

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal("2026.09.01", result.Info!.Version.ToString());
    }

    [Fact]
    public async Task CheckAppAsync_ResolverLiefertNull_AbgelaufenerCache_AlterWertWirdVerwendet()
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
        var service = new UpdateCheckService(resolver, cache, () => Now);

        var result = await service.CheckAppAsync(force: false, CancellationToken.None);

        Assert.True(result.FromCache);
        Assert.Equal("2026.06.01", result.Info!.Version.ToString());
    }

    [Fact]
    public async Task CheckAppAsync_ResolverLiefertNull_KeinCache_LiefertNull()
    {
        var cache = MakeCache();
        var resolver = new FakeReleaseResolver(result: null);
        var service = new UpdateCheckService(resolver, cache, () => Now);

        var result = await service.CheckAppAsync(force: false, CancellationToken.None);

        Assert.Null(result.Info);
        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public async Task CheckAppAsync_DefekteCacheDatei_WieKeinCache_KeineAusnahme()
    {
        await File.WriteAllTextAsync(_cacheFilePath, "{kaputtes json");
        var cache = MakeCache();
        var resolver = new FakeReleaseResolver(MakeInfo("2026.09.01"));
        var service = new UpdateCheckService(resolver, cache, () => Now);

        var exception = await Record.ExceptionAsync(() => service.CheckAppAsync(force: false, CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal(1, resolver.CallCount);
    }

    [Fact]
    public async Task CheckAppAsync_GemeldeteVersionGleichOderAelter_UpdateAvailableIstFalse()
    {
        var cache = MakeCache();
        var resolver = new FakeReleaseResolver(MakeInfo("2026.06.01"));
        var service = new UpdateCheckService(resolver, cache, () => Now);

        var result = await service.CheckAppAsync(force: false, CancellationToken.None);

        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public async Task CheckAppAsync_ZeitstempelAusDerZukunft_GiltAlsAbgelaufen_ResolverWirdAufgerufen()
    {
        var cache = MakeCache();
        await cache.WriteAsync("app", new UpdateCacheEntry
        {
            CheckedUtc = Now + TimeSpan.FromHours(1),
            Version = "2026.06.01",
            Source = "github-api-latest",
        }, CancellationToken.None);

        var resolver = new FakeReleaseResolver(MakeInfo("2026.09.01"));
        var service = new UpdateCheckService(resolver, cache, () => Now);

        var result = await service.CheckAppAsync(force: false, CancellationToken.None);

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal("2026.09.01", result.Info!.Version.ToString());
    }

    [Fact]
    public async Task CheckAppAsync_EigeneVersionUnbekannt_ResolverWirdNichtAufgerufen()
    {
        AppInfo.CurrentVersion = null;
        var cache = MakeCache();
        var resolver = new FakeReleaseResolver(MakeInfo("2026.09.01"));
        var service = new UpdateCheckService(resolver, cache, () => Now);

        var result = await service.CheckAppAsync(force: false, CancellationToken.None);

        Assert.Equal(0, resolver.CallCount);
        Assert.Null(result.Info);
        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public async Task CheckAppAsync_304_CacheBleibtGueltigUndLaufzeitBeginntNeu()
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
        var service = new UpdateCheckService(resolver, cache, () => Now);

        var result = await service.CheckAppAsync(force: false, CancellationToken.None);

        Assert.True(result.FromCache);
        Assert.Equal("2026.07.01", result.Info!.Version.ToString());

        var updated = await cache.ReadAsync("app", CancellationToken.None);
        Assert.Equal(Now, updated!.CheckedUtc);
        Assert.Equal("2026.07.01", updated.Version);
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
