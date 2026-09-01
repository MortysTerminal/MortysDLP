using MortysDLP.Services.Tools;
using System.IO;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft die reinen Aufräumfunktionen gegen ein echtes Temp-Verzeichnis mit künstlich
/// gealterten Dateien (<see cref="File.SetLastWriteTimeUtc"/>) — keine Abhängigkeit von der
/// tatsächlichen Uhrzeit oder von echten Werkzeugen.
/// </summary>
public class ToolHousekeepingTests : IDisposable
{
    private readonly string _tempDir;

    public ToolHousekeepingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.Housekeeping", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private string MakeFile(string relativePath, DateTime lastWriteTime)
    {
        string path = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        File.SetLastWriteTime(path, lastWriteTime);
        return path;
    }

    // ── CleanupFilesByAge ───────────────────────────────────────────────────────

    [Fact]
    public void CleanupFilesByAge_AeltereDateiWirdEntfernt_JuengereBleibt()
    {
        var now = DateTime.Now;
        string old = MakeFile("ffmpeg.exe.part", now.AddHours(-25));
        string fresh = MakeFile("yt-dlp.exe.part", now.AddHours(-1));

        var deleted = ToolHousekeeping.CleanupFilesByAge(
            _tempDir, "*.part", now, ToolHousekeeping.PartFileMaxAge, "Test");

        Assert.Single(deleted);
        Assert.Equal(old, deleted[0].Path);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(fresh));
    }

    [Fact]
    public void CleanupFilesByAge_DurchsuchtUnterordnerRekursiv()
    {
        var now = DateTime.Now;
        string modelPart = MakeFile(Path.Combine("Whisper", "models", "ggml-tiny.bin.part"), now.AddHours(-25));

        var deleted = ToolHousekeeping.CleanupFilesByAge(
            _tempDir, "*.part", now, ToolHousekeeping.PartFileMaxAge, "Test");

        Assert.Single(deleted);
        Assert.Equal(modelPart, deleted[0].Path);
    }

    [Fact]
    public void CleanupFilesByAge_NichtsZuLoeschen_LiefertLeereListe()
    {
        MakeFile("ffmpeg.exe.part", DateTime.Now.AddHours(-1));

        var deleted = ToolHousekeeping.CleanupFilesByAge(
            _tempDir, "*.part", DateTime.Now, ToolHousekeeping.PartFileMaxAge, "Test");

        Assert.Empty(deleted);
    }

    [Fact]
    public void CleanupFilesByAge_VerzeichnisFehlt_WirftNichtUndLiefertLeereListe()
    {
        string missing = Path.Combine(_tempDir, "does-not-exist");

        var deleted = ToolHousekeeping.CleanupFilesByAge(
            missing, "*.part", DateTime.Now, ToolHousekeeping.PartFileMaxAge, "Test");

        Assert.Empty(deleted);
    }

    [Fact]
    public void CleanupFilesByAge_AeltereOldDateiWirdEntfernt_JuengereBleibt()
    {
        var now = DateTime.Now;
        string old = MakeFile("ffmpeg.exe.old", now.AddDays(-8));
        string fresh = MakeFile("yt-dlp.exe.old", now.AddDays(-1));

        var deleted = ToolHousekeeping.CleanupFilesByAge(
            _tempDir, "*.old", now, ToolHousekeeping.BackupFileMaxAge, "Test");

        Assert.Single(deleted);
        Assert.Equal(old, deleted[0].Path);
        Assert.True(File.Exists(fresh));
    }

    [Fact]
    public void CleanupFilesByAge_GesperrteDateiWirdUebersprungenStattZuWerfen()
    {
        var now = DateTime.Now;
        string locked = MakeFile("ffmpeg.exe.part", now.AddHours(-25));

        using var stream = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read);

        var ex = Record.Exception(() => ToolHousekeeping.CleanupFilesByAge(
            _tempDir, "*.part", now, ToolHousekeeping.PartFileMaxAge, "Test"));

        Assert.Null(ex);
    }

    // ── CleanupOwnTempResidue ───────────────────────────────────────────────────

    [Fact]
    public void CleanupOwnTempResidue_AeltereDateiWirdEntfernt_JuengereBleibt()
    {
        var now = DateTime.Now;
        string old = MakeFile("ffmpeg-abc.zip", now.AddHours(-25));
        string fresh = MakeFile("whisper-def.zip", now.AddHours(-1));

        var deleted = ToolHousekeeping.CleanupOwnTempResidue(_tempDir, now, ToolHousekeeping.OwnTempResidueMaxAge);

        Assert.Single(deleted);
        Assert.Equal(old, deleted[0].Path);
        Assert.True(File.Exists(fresh));
    }

    [Fact]
    public void CleanupOwnTempResidue_VerwaisterOrdnerWirdRekursivEntfernt()
    {
        var now = DateTime.Now;
        string nested = MakeFile(Path.Combine("extract_abc", "ffmpeg.exe"), now.AddHours(-25));
        Directory.SetLastWriteTime(Path.Combine(_tempDir, "extract_abc"), now.AddHours(-25));

        var deleted = ToolHousekeeping.CleanupOwnTempResidue(_tempDir, now, ToolHousekeeping.OwnTempResidueMaxAge);

        Assert.Single(deleted);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "extract_abc")));
        Assert.False(File.Exists(nested));
    }

    [Fact]
    public void CleanupOwnTempResidue_VerzeichnisFehlt_WirftNichtUndLiefertLeereListe()
    {
        string missing = Path.Combine(_tempDir, "does-not-exist");

        var deleted = ToolHousekeeping.CleanupOwnTempResidue(missing, DateTime.Now, ToolHousekeeping.OwnTempResidueMaxAge);

        Assert.Empty(deleted);
    }

    // ── RunAll ──────────────────────────────────────────────────────────────────

    [Fact]
    public void RunAll_GegenEchtePfade_WirftNicht()
    {
        // ToolsDir/ToolTempDir des echten Systems könnten Restdateien enthalten - dieser Test
        // prüft nur, dass der Aufruf selbst nicht wirft, nicht den genauen Inhalt.
        var ex = Record.Exception(() => ToolHousekeeping.RunAll(DateTime.Now));

        Assert.Null(ex);
    }
}
