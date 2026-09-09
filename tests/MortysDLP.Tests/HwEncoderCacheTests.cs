using MortysDLP.Services;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="HwEncoderCache"/>: Lesen/Schreiben, defekte Dateien, atomares Schreiben
/// und den Versionsabgleich (<see cref="HwEncoderCache.Matches"/>) — jeweils gegen ein eigenes
/// Temp-Verzeichnis, ohne die echte Anwendungsablage zu berühren.
/// </summary>
public class HwEncoderCacheTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public HwEncoderCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.HwEncoderCache", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "hw-encoder.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private static HwEncoderCacheEntry MakeEntry(
        string encoder = "h264_nvenc",
        string ffmpegVersion = "7.1-essentials_build-www.gyan.dev") => new()
    {
        Encoder = encoder,
        FfmpegVersion = ffmpegVersion,
        DetectedUtc = DateTimeOffset.Parse("2026-09-09T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
    };

    [Fact]
    public async Task ReadAsync_DateiExistiertNicht_LiefertNull()
    {
        var cache = new HwEncoderCache(_filePath);

        var entry = await cache.ReadAsync(CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task WriteAsync_DannReadAsync_LiefertDenselbenEintrag()
    {
        var cache = new HwEncoderCache(_filePath);
        var entry = MakeEntry();

        await cache.WriteAsync(entry, CancellationToken.None);
        var read = await cache.ReadAsync(CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal(entry.Encoder, read!.Encoder);
        Assert.Equal(entry.FfmpegVersion, read.FfmpegVersion);
        Assert.Equal(entry.DetectedUtc, read.DetectedUtc);
    }

    [Fact]
    public async Task WriteAsync_SchreibtKeineZurueckbleibendeTmpDatei()
    {
        var cache = new HwEncoderCache(_filePath);

        await cache.WriteAsync(MakeEntry(), CancellationToken.None);

        Assert.True(File.Exists(_filePath));
        Assert.False(File.Exists(_filePath + ".tmp"));
    }

    [Fact]
    public async Task WriteAsync_ZweimalGeschrieben_LetzterEintragGilt()
    {
        var cache = new HwEncoderCache(_filePath);

        await cache.WriteAsync(MakeEntry("h264_nvenc"), CancellationToken.None);
        await cache.WriteAsync(MakeEntry("h264_qsv"), CancellationToken.None);

        var read = await cache.ReadAsync(CancellationToken.None);
        Assert.Equal("h264_qsv", read!.Encoder);
    }

    [Fact]
    public async Task ReadAsync_KaputtesJson_LiefertNullOhneWurf()
    {
        await File.WriteAllTextAsync(_filePath, "{kaputtes json");
        var cache = new HwEncoderCache(_filePath);

        var exception = await Record.ExceptionAsync(() => cache.ReadAsync(CancellationToken.None));
        var entry = await cache.ReadAsync(CancellationToken.None);

        Assert.Null(exception);
        Assert.Null(entry);
    }

    [Fact]
    public async Task ReadAsync_LeereDatei_LiefertNullOhneWurf()
    {
        await File.WriteAllTextAsync(_filePath, "");
        var cache = new HwEncoderCache(_filePath);

        var entry = await cache.ReadAsync(CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task ReadAsync_FalscheSchemaVersion_LiefertNull()
    {
        await File.WriteAllTextAsync(_filePath,
            """{"schemaVersion":99,"entry":{"encoder":"h264_nvenc","ffmpegVersion":"7.1"}}""");
        var cache = new HwEncoderCache(_filePath);

        var entry = await cache.ReadAsync(CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task ReadAsync_FremdeJsonStruktur_LiefertNullOhneWurf()
    {
        await File.WriteAllTextAsync(_filePath, """["das","ist","ein","array"]""");
        var cache = new HwEncoderCache(_filePath);

        var entry = await cache.ReadAsync(CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task WriteAsync_GesperrtesVerzeichnis_WirftNicht()
    {
        // Ein Dateiname anstelle eines Verzeichnisses lässt CreateDirectory scheitern.
        string blockedPath = Path.Combine(_tempDir, "blocked-by-file");
        File.WriteAllText(blockedPath, "ich bin eine Datei, kein Ordner");
        var cache = new HwEncoderCache(Path.Combine(blockedPath, "hw-encoder.json"));

        var exception = await Record.ExceptionAsync(
            () => cache.WriteAsync(MakeEntry(), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ClearAsync_VorhandeneDatei_WirdGeloescht()
    {
        var cache = new HwEncoderCache(_filePath);
        await cache.WriteAsync(MakeEntry(), CancellationToken.None);

        await cache.ClearAsync(CancellationToken.None);

        Assert.False(File.Exists(_filePath));
    }

    [Fact]
    public async Task ClearAsync_KeineDatei_WirftNicht()
    {
        var cache = new HwEncoderCache(_filePath);

        var exception = await Record.ExceptionAsync(() => cache.ClearAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    // --- Versionsabgleich (Matches) --------------------------------------------------------

    [Fact]
    public void Matches_GleicheVersion_IstWahr()
    {
        Assert.True(HwEncoderCache.Matches(MakeEntry(ffmpegVersion: "7.1-gyan"), "7.1-gyan"));
    }

    [Fact]
    public void Matches_AndereVersion_IstFalsch()
    {
        // Nach einem ffmpeg-Update darf der gemerkte Encoder nicht mehr gelten.
        Assert.False(HwEncoderCache.Matches(MakeEntry(ffmpegVersion: "7.0-gyan"), "7.1-gyan"));
    }

    [Fact]
    public void Matches_NullEintrag_IstFalsch()
    {
        Assert.False(HwEncoderCache.Matches(null, "7.1-gyan"));
    }

    [Fact]
    public void Matches_EintragOhneEncoder_IstFalsch()
    {
        Assert.False(HwEncoderCache.Matches(MakeEntry(encoder: ""), "7.1-gyan"));
    }

    [Fact]
    public void Matches_AktuelleVersionUnbekannt_IstFalsch()
    {
        Assert.False(HwEncoderCache.Matches(MakeEntry(ffmpegVersion: "7.1-gyan"), null));
    }

    [Fact]
    public async Task WriteAsync_DannMatches_ErkenntGemerktesFfmpeg()
    {
        var cache = new HwEncoderCache(_filePath);
        await cache.WriteAsync(MakeEntry(encoder: "h264_amf", ffmpegVersion: "7.1-x"), CancellationToken.None);

        var read = await cache.ReadAsync(CancellationToken.None);

        Assert.True(HwEncoderCache.Matches(read, "7.1-x"));
        Assert.False(HwEncoderCache.Matches(read, "7.2-x"));
    }
}
