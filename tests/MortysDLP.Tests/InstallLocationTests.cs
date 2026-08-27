using MortysDLP.Helpers;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft die reine Pfadauswertung von <see cref="InstallLocation"/> mit erfundenen Pfaden —
/// ohne Dateisystemzugriff. Der Schreibtest (<c>CanWriteTo</c>) ist privat und damit bewusst
/// nicht Teil dieser Suite; er wird indirekt über die Handtests (A2, A11, A12) abgedeckt.
/// </summary>
public class InstallLocationTests
{
    [Theory]
    [InlineData(@"C:\Users\mbo\AppData\Local\Temp\Temp1_MortysDLP.zip\MortysDLP")]
    [InlineData(@"C:\Users\mbo\AppData\Local\Temp\Temp23_MortysDLP.zip")]
    public void IsRunningFromArchive_ExplorerZipVorschauOrdner_ErkanntAlsArchiv(string path)
    {
        Assert.True(InstallLocation.IsRunningFromArchive(path));
    }

    [Fact]
    public void IsRunningFromArchive_UnterhalbDesTempOrdners_ErkanntAlsArchiv()
    {
        string path = Path.Combine(Path.GetTempPath(), "MortysDLP");

        Assert.True(InstallLocation.IsRunningFromArchive(path));
    }

    [Theory]
    [InlineData(@"C:\Users\mbo\AppData\Local\Temp\7zO1234\MortysDLP")]
    [InlineData(@"C:\Users\mbo\AppData\Local\Temp\Rar$EXa0.123\MortysDLP")]
    public void IsRunningFromArchive_AndereEntpacktools_ErkanntAlsArchiv(string path)
    {
        Assert.True(InstallLocation.IsRunningFromArchive(path));
    }

    [Theory]
    [InlineData(@"C:\Tools\MortysDLP")]
    [InlineData(@"C:\Users\mbo\Desktop\MortysDLP")]
    [InlineData(@"D:\Portable\MortysDLP")]
    public void IsRunningFromArchive_NormalerOrdner_NichtErkanntAlsArchiv(string path)
    {
        Assert.False(InstallLocation.IsRunningFromArchive(path));
    }

    [Fact]
    public void IsProtectedSystemFolder_ProgramFiles_ErkanntAlsGeschuetzt()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MortysDLP");

        Assert.True(InstallLocation.IsProtectedSystemFolder(path));
    }

    [Fact]
    public void IsProtectedSystemFolder_WindowsOrdnerSelbst_ErkanntAlsGeschuetzt()
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        Assert.True(InstallLocation.IsProtectedSystemFolder(path));
    }

    [Theory]
    [InlineData(@"C:\Tools\MortysDLP")]
    [InlineData(@"D:\Portable\MortysDLP")]
    public void IsProtectedSystemFolder_NormalerOrdner_NichtErkanntAlsGeschuetzt(string path)
    {
        Assert.False(InstallLocation.IsProtectedSystemFolder(path));
    }

    [Fact]
    public void IsProtectedSystemFolder_AehnlicherAberEigenstaendigerOrdnername_NichtErkanntAlsGeschuetzt()
    {
        // "C:\Program Files-Backup" beginnt mit demselben Text wie "C:\Program Files",
        // ist aber ein eigener Ordner - die Prüfung muss auf Ordnergrenzen achten.
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string path = programFiles.TrimEnd(Path.DirectorySeparatorChar) + "-Backup";

        Assert.False(InstallLocation.IsProtectedSystemFolder(path));
    }

    [Fact]
    public void Analyze_MitExplizitemArchivPfad_LiefertRunningFromArchive()
    {
        string path = Path.Combine(Path.GetTempPath(), "Temp1_MortysDLP.zip", "MortysDLP");

        var info = InstallLocation.Analyze(path);

        Assert.Equal(InstallKind.RunningFromArchive, info.Kind);
        Assert.False(info.CanSelfUpdate);
        Assert.False(info.CanUpdateTools);
        Assert.Equal("InstallLocation.Warning.Archive", info.ReasonKey);
    }

    [Fact]
    public void Analyze_MitExplizitemSystemordnerPfad_LiefertNeedsElevation()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MortysDLP");

        var info = InstallLocation.Analyze(path);

        Assert.Equal(InstallKind.NeedsElevation, info.Kind);
        Assert.False(info.CanSelfUpdate);
        Assert.False(info.CanUpdateTools);
        Assert.Equal("InstallLocation.Warning.Elevation", info.ReasonKey);
    }

    [Fact]
    public void Analyze_MitBeschreibbaremOrdner_LiefertWritable()
    {
        // Bewusst NICHT unter Path.GetTempPath(): das würde die Archiv-Heuristik auslösen,
        // die genau das erkennen soll und daher ihrerseits vor dem Schreibtest greift.
        string tempDir = Path.Combine(AppContext.BaseDirectory, "InstallLocationTestScratch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var info = InstallLocation.Analyze(tempDir);

            Assert.Equal(InstallKind.Writable, info.Kind);
            Assert.True(info.CanSelfUpdate);
            Assert.True(info.CanUpdateTools);
            Assert.Equal("", info.ReasonKey);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* Best-Effort */ }
        }
    }

    [Fact]
    public void Analyze_MitBeschreibbaremOrdner_HinterlaesstKeineTestdatei()
    {
        // Bewusst NICHT unter Path.GetTempPath(): das würde die Archiv-Heuristik auslösen,
        // die genau das erkennen soll und daher ihrerseits vor dem Schreibtest greift.
        string tempDir = Path.Combine(AppContext.BaseDirectory, "InstallLocationTestScratch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            InstallLocation.Analyze(tempDir);

            Assert.Empty(Directory.GetFiles(tempDir));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* Best-Effort */ }
        }
    }

    [Fact]
    public void DescribeForLog_EnthaeltPfadUndKind()
    {
        var info = new InstallInfo(InstallKind.Writable, @"C:\Tools\MortysDLP", true, true, "");

        string description = InstallLocation.DescribeForLog(info);

        Assert.Contains(@"C:\Tools\MortysDLP", description);
        Assert.Contains("Writable", description);
    }

    [Fact]
    public void DescribeForLog_UncPfad_AlsNetzwerkErkannt()
    {
        // Bekannter, bewusst fehlschlagender Test - siehe Befund W-10 in werkstatt/ANALYSE.md.
        // Path.GetPathRoot liefert für UNC-Pfade korrekt "\\server\share", aber DriveInfo
        // akzeptiert nur Laufwerksbuchstaben/"C:\"-Wurzeln und wirft eine ArgumentException.
        // DescribeForLog fängt das ab und meldet dann keine Laufwerksdetails - UNC-Netzpfade
        // werden dadurch nicht als "Network" erkannt, wie es diese Aufgabe verlangt.
        var info = new InstallInfo(InstallKind.Writable, @"\\server\share\MortysDLP", true, true, "");

        string description = InstallLocation.DescribeForLog(info);

        Assert.Contains("Network", description);
    }
}
