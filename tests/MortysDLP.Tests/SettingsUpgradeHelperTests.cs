using MortysDLP.Helpers;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft die reine Verzeichnislogik hinter der Protokollzeile zu
/// <c>Settings.Default.Upgrade()</c> — mit erfundenen Ordnern, ohne echte
/// <c>user.config</c>. Siehe <c>werkstatt/tasks/W2-T02.md</c>, Schritt 4.
/// </summary>
public class SettingsUpgradeHelperTests
{
    private static string CreateHashDir(params string[] versionFolders)
    {
        string hashDir = Path.Combine(
            AppContext.BaseDirectory, "SettingsUpgradeTestScratch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(hashDir);

        foreach (string version in versionFolders)
            Directory.CreateDirectory(Path.Combine(hashDir, version));

        return hashDir;
    }

    [Fact]
    public void FindPreviousVersionDirectory_MehrereVorgaenger_LiefertHoechsteUnterhalbDerAktuellen()
    {
        string hashDir = CreateHashDir("1.0.0.0", "1.2.0.0", "2.0.0.0", "2.6.1.0");
        try
        {
            string? result = SettingsUpgradeHelper.FindPreviousVersionDirectory(hashDir, new Version(2, 6, 1, 0));

            Assert.Equal(Path.Combine(hashDir, "2.0.0.0"), result);
        }
        finally
        {
            Directory.Delete(hashDir, recursive: true);
        }
    }

    [Fact]
    public void FindPreviousVersionDirectory_GleicheVersionVorhanden_WirdAusgeschlossen()
    {
        string hashDir = CreateHashDir("1.0.0.0", "2.6.1.0");
        try
        {
            string? result = SettingsUpgradeHelper.FindPreviousVersionDirectory(hashDir, new Version(2, 6, 1, 0));

            Assert.Equal(Path.Combine(hashDir, "1.0.0.0"), result);
        }
        finally
        {
            Directory.Delete(hashDir, recursive: true);
        }
    }

    [Fact]
    public void FindPreviousVersionDirectory_KeineVorgaengerversion_LiefertNull()
    {
        string hashDir = CreateHashDir("2.6.1.0", "3.0.0.0");
        try
        {
            string? result = SettingsUpgradeHelper.FindPreviousVersionDirectory(hashDir, new Version(2, 6, 1, 0));

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(hashDir, recursive: true);
        }
    }

    [Fact]
    public void FindPreviousVersionDirectory_NichtVersionsfoermigeOrdner_WerdenIgnoriert()
    {
        string hashDir = CreateHashDir("1.0.0.0", "cache", "backup-old");
        try
        {
            string? result = SettingsUpgradeHelper.FindPreviousVersionDirectory(hashDir, new Version(2, 0, 0, 0));

            Assert.Equal(Path.Combine(hashDir, "1.0.0.0"), result);
        }
        finally
        {
            Directory.Delete(hashDir, recursive: true);
        }
    }

    [Fact]
    public void FindPreviousVersionDirectory_OrdnerFehlt_LiefertNull()
    {
        string hashDir = Path.Combine(
            AppContext.BaseDirectory, "SettingsUpgradeTestScratch", Guid.NewGuid().ToString("N"));

        string? result = SettingsUpgradeHelper.FindPreviousVersionDirectory(hashDir, new Version(2, 0, 0, 0));

        Assert.Null(result);
    }

    [Fact]
    public void FindPreviousVersionDirectory_LeererOrdner_LiefertNull()
    {
        string hashDir = CreateHashDir();
        try
        {
            string? result = SettingsUpgradeHelper.FindPreviousVersionDirectory(hashDir, new Version(1, 0, 0, 0));

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(hashDir, recursive: true);
        }
    }
}
