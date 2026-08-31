using MortysDLP.Helpers;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="ToolsMigration.Migrate"/> mit selbst angelegten Ordnern unter
/// <see cref="AppContext.BaseDirectory"/> — niemals mit dem echten Nutzerprofil, sonst würde
/// ein Test dort tatsächlich Dateien verschieben.
/// </summary>
public class ToolsMigrationTests
{
    private static (string OldDir, string NewDir, string Root) CreateScratchDirs([System.Runtime.CompilerServices.CallerMemberName] string? testName = null)
    {
        string root = Path.Combine(AppContext.BaseDirectory, "ToolsMigrationScratch", $"{testName}_{Guid.NewGuid():N}");
        string oldDir = Path.Combine(root, "alt");
        string newDir = Path.Combine(root, "neu");
        Directory.CreateDirectory(oldDir);
        return (oldDir, newDir, root);
    }

    [Fact]
    public void Migrate_AlterOrdnerFehlt_LiefertLeeresErgebnisOhneNeuenOrdnerAnzulegen()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "ToolsMigrationScratch", Guid.NewGuid().ToString("N"));
        string oldDir = Path.Combine(root, "alt");
        string newDir = Path.Combine(root, "neu");

        var result = ToolsMigration.Migrate(oldDir, newDir);

        Assert.Empty(result.MigratedFiles);
        Assert.Empty(result.DuplicatedFiles);
        Assert.Empty(result.FailedFiles);
        Assert.False(result.OldDirRemoved);
        Assert.False(Directory.Exists(newDir));
    }

    [Fact]
    public void Migrate_LeererAlterOrdnerNurGitkeep_WirdEntferntUndNichtsWirdUebernommen()
    {
        var (oldDir, newDir, root) = CreateScratchDirs();
        try
        {
            File.WriteAllText(Path.Combine(oldDir, ".gitkeep"), "");

            var result = ToolsMigration.Migrate(oldDir, newDir);

            Assert.Empty(result.MigratedFiles);
            Assert.Empty(result.DuplicatedFiles);
            Assert.Empty(result.FailedFiles);
            Assert.True(result.OldDirRemoved);
            Assert.False(Directory.Exists(oldDir));
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    }

    [Fact]
    public void Migrate_VorhandenesWerkzeug_WirdVerschobenUndAlterOrdnerEntfernt()
    {
        var (oldDir, newDir, root) = CreateScratchDirs();
        try
        {
            string oldFile = Path.Combine(oldDir, "yt-dlp.exe");
            File.WriteAllText(oldFile, "fake-exe");

            var result = ToolsMigration.Migrate(oldDir, newDir);

            Assert.Contains("yt-dlp.exe", result.MigratedFiles);
            Assert.Empty(result.DuplicatedFiles);
            Assert.Empty(result.FailedFiles);
            Assert.True(result.OldDirRemoved);
            Assert.False(File.Exists(oldFile));
            Assert.True(File.Exists(Path.Combine(newDir, "yt-dlp.exe")));
            Assert.Equal("fake-exe", File.ReadAllText(Path.Combine(newDir, "yt-dlp.exe")));
            Assert.False(Directory.Exists(oldDir));
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    }

    [Fact]
    public void Migrate_UnterordnerWieWhisperModelle_WerdenMitStrukturUebernommen()
    {
        var (oldDir, newDir, root) = CreateScratchDirs();
        try
        {
            string modelsDir = Path.Combine(oldDir, "Whisper", "models");
            Directory.CreateDirectory(modelsDir);
            File.WriteAllText(Path.Combine(modelsDir, "ggml-base.bin"), "fake-model");

            var result = ToolsMigration.Migrate(oldDir, newDir);

            string expectedRelative = Path.Combine("Whisper", "models", "ggml-base.bin");
            Assert.Contains(expectedRelative, result.MigratedFiles);
            Assert.True(File.Exists(Path.Combine(newDir, "Whisper", "models", "ggml-base.bin")));
            Assert.True(result.OldDirRemoved);
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    }

    [Fact]
    public void Migrate_ZieldateiExistiertBereits_AlteDateiBleibtAlsRestLiegen()
    {
        var (oldDir, newDir, root) = CreateScratchDirs();
        try
        {
            Directory.CreateDirectory(newDir);
            File.WriteAllText(Path.Combine(oldDir, "ffmpeg.exe"), "alt");
            File.WriteAllText(Path.Combine(newDir, "ffmpeg.exe"), "bereits-vorhanden");

            var result = ToolsMigration.Migrate(oldDir, newDir);

            Assert.Contains("ffmpeg.exe", result.FailedFiles);
            Assert.Empty(result.MigratedFiles);
            Assert.False(result.OldDirRemoved);
            Assert.True(File.Exists(Path.Combine(oldDir, "ffmpeg.exe")));
            Assert.Equal("bereits-vorhanden", File.ReadAllText(Path.Combine(newDir, "ffmpeg.exe")));
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    }

    [Fact]
    public void Migrate_GesperrteDatei_WirdStattdessenKopiertUndBleibtAmAltenOrtLiegen()
    {
        var (oldDir, newDir, root) = CreateScratchDirs();
        try
        {
            string oldFile = Path.Combine(oldDir, "yt-dlp.exe");
            File.WriteAllText(oldFile, "fake-exe");

            // Ein offener Lesehandle verhindert unter Windows das Umbenennen (File.Move),
            // erlaubt aber weiterhin Lesezugriff für File.Copy - genau der Fall, den
            // Migrate() über den Fallback abfangen soll.
            using (new FileStream(oldFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var result = ToolsMigration.Migrate(oldDir, newDir);

                Assert.Contains("yt-dlp.exe", result.DuplicatedFiles);
                Assert.Empty(result.FailedFiles);
                Assert.False(result.OldDirRemoved);
                Assert.True(File.Exists(oldFile));
                Assert.True(File.Exists(Path.Combine(newDir, "yt-dlp.exe")));
            }
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    }
}
