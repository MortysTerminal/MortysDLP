using MortysDLP.Helpers;
using MortysDLP.Services;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="UpdateCache"/>: Lesen/Schreiben, defekte Dateien, atomares Schreiben —
/// jeweils gegen ein eigenes Temp-Verzeichnis, ohne die echte Anwendungsablage zu berühren.
/// Siehe <c>werkstatt/tasks/W2-T06.md</c>.
/// </summary>
public class UpdateCacheTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public UpdateCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.UpdateCache", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "update-cache.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private static UpdateCacheEntry MakeEntry(string version = "2026.06.01") => new()
    {
        CheckedUtc = DateTimeOffset.Parse("2026-08-27T09:12:00Z", System.Globalization.CultureInfo.InvariantCulture),
        Version = version,
        DownloadUrl = "https://github.com/MortysTerminal/MortysDLP/releases/download/2026.06.01/MortysDLP.zip",
        Changelog = "Changelog-Text",
        ETag = "\"abc123\"",
        Source = "github-api-latest",
    };

    [Fact]
    public async Task ReadAsync_DateiExistiertNicht_LiefertNull()
    {
        var cache = new UpdateCache(_filePath);

        var entry = await cache.ReadAsync("app", CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task WriteAsync_DannReadAsync_LiefertDenselbenEintrag()
    {
        var cache = new UpdateCache(_filePath);
        var entry = MakeEntry();

        await cache.WriteAsync("app", entry, CancellationToken.None);
        var read = await cache.ReadAsync("app", CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal(entry.Version, read!.Version);
        Assert.Equal(entry.DownloadUrl, read.DownloadUrl);
        Assert.Equal(entry.Changelog, read.Changelog);
        Assert.Equal(entry.ETag, read.ETag);
        Assert.Equal(entry.Source, read.Source);
        Assert.Equal(entry.CheckedUtc, read.CheckedUtc);
    }

    [Fact]
    public async Task WriteAsync_SchreibtKeineZurueckbleibendeTmpDatei()
    {
        var cache = new UpdateCache(_filePath);

        await cache.WriteAsync("app", MakeEntry(), CancellationToken.None);

        Assert.True(File.Exists(_filePath));
        Assert.False(File.Exists(_filePath + ".tmp"));
    }

    [Fact]
    public async Task ReadAsync_UnbekannterSchluessel_LiefertNull()
    {
        var cache = new UpdateCache(_filePath);
        await cache.WriteAsync("app", MakeEntry(), CancellationToken.None);

        var entry = await cache.ReadAsync("yt-dlp", CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task ReadAsync_KaputtesJson_LiefertNullOhneWurf()
    {
        await File.WriteAllTextAsync(_filePath, "{kaputtes json");
        var cache = new UpdateCache(_filePath);

        var exception = await Record.ExceptionAsync(() => cache.ReadAsync("app", CancellationToken.None));
        var entry = await cache.ReadAsync("app", CancellationToken.None);

        Assert.Null(exception);
        Assert.Null(entry);
    }

    [Fact]
    public async Task ReadAsync_LeereDatei_LiefertNullOhneWurf()
    {
        await File.WriteAllTextAsync(_filePath, "");
        var cache = new UpdateCache(_filePath);

        var entry = await cache.ReadAsync("app", CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task ReadAsync_FalscheSchemaVersion_LiefertNull()
    {
        await File.WriteAllTextAsync(_filePath, """{"schemaVersion":2,"entries":{"app":{"version":"2026.06.01"}}}""");
        var cache = new UpdateCache(_filePath);

        var entry = await cache.ReadAsync("app", CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task ReadAsync_FremdeJsonStruktur_LiefertNullOhneWurf()
    {
        await File.WriteAllTextAsync(_filePath, """["das", "ist", "ein", "array"]""");
        var cache = new UpdateCache(_filePath);

        var entry = await cache.ReadAsync("app", CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task WriteAsync_SchreibgeschuetztesVerzeichnis_WirftNicht()
    {
        // Ein Dateiname anstelle eines Verzeichnisses lässt CreateDirectory scheitern -
        // derselbe Trick wie in LogTests für "gesperrtes Verzeichnis".
        string blockedPath = Path.Combine(_tempDir, "blocked-by-file");
        File.WriteAllText(blockedPath, "ich bin eine Datei, kein Ordner");
        string cacheFileInBlockedDir = Path.Combine(blockedPath, "update-cache.json");
        var cache = new UpdateCache(cacheFileInBlockedDir);

        var exception = await Record.ExceptionAsync(
            () => cache.WriteAsync("app", MakeEntry(), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task WriteAsync_ZweiSchluessel_BeideBleibenErhalten()
    {
        var cache = new UpdateCache(_filePath);

        await cache.WriteAsync("app", MakeEntry("2026.06.01"), CancellationToken.None);
        await cache.WriteAsync("yt-dlp", MakeEntry("2026.08.20"), CancellationToken.None);

        var app = await cache.ReadAsync("app", CancellationToken.None);
        var ytDlp = await cache.ReadAsync("yt-dlp", CancellationToken.None);

        Assert.Equal("2026.06.01", app!.Version);
        Assert.Equal("2026.08.20", ytDlp!.Version);
    }

    [Fact]
    public async Task ClearAsync_VorhandeneDatei_WirdGeloescht()
    {
        var cache = new UpdateCache(_filePath);
        await cache.WriteAsync("app", MakeEntry(), CancellationToken.None);

        await cache.ClearAsync(CancellationToken.None);

        Assert.False(File.Exists(_filePath));
    }

    [Fact]
    public async Task ClearAsync_VorhandeneDatei_ProtokolliertErfolg()
    {
        string logDir = Path.Combine(_tempDir, "logs");
        Log.LogsDirectory = logDir;
        Log.MinLevel = LogLevel.Debug;

        var cache = new UpdateCache(_filePath);
        await cache.WriteAsync("app", MakeEntry(), CancellationToken.None);

        await cache.ClearAsync(CancellationToken.None);
        Log.CloseForTests();

        string content = File.ReadAllText(Log.CurrentLogFile);
        Assert.Contains("Update-Zwischenspeicher geleert", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClearAsync_KeineDatei_WirftNicht()
    {
        var cache = new UpdateCache(_filePath);

        var exception = await Record.ExceptionAsync(() => cache.ClearAsync(CancellationToken.None));

        Assert.Null(exception);
    }
}
