using MortysDLP.Helpers;
using System.IO;
using System.Text.RegularExpressions;

namespace MortysDLP.Tests;

/// <summary>
/// Tests laufen sequenziell innerhalb dieser Klasse (xUnit-Standardverhalten), deshalb ist das
/// Umschalten von <see cref="Log.LogsDirectory"/> und <see cref="Log.MinLevel"/> zwischen den
/// Tests unproblematisch. Jeder Test bekommt trotzdem sein eigenes Temp-Verzeichnis.
/// </summary>
public class LogTests : IDisposable
{
    private static readonly Regex LineRegex = new(
        @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[(DEBUG|INFO |WARN |ERROR)\] .+: .+$",
        RegexOptions.Compiled);

    private readonly string _tempDir;

    public LogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.Log", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Log.LogsDirectory = _tempDir;
        Log.MinLevel = LogLevel.Debug;
    }

    public void Dispose()
    {
        Log.CloseForTests();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Info_SchreibtZeileInsAktuelleProtokoll()
    {
        Log.Info("Testnachricht");
        Log.CloseForTests();

        Assert.True(File.Exists(Log.CurrentLogFile));
        string content = File.ReadAllText(Log.CurrentLogFile);
        Assert.Contains("[INFO ]", content);
        Assert.Contains("Testnachricht", content);
    }

    [Fact]
    public void MehrereThreads_SchreibenKeineKaputtenZeilen()
    {
        const int threadCount = 20;
        const int messagesPerThread = 50;

        Parallel.For(0, threadCount, i =>
        {
            for (int j = 0; j < messagesPerThread; j++)
                Log.Info($"Thread {i} Nachricht {j}");
        });

        Log.CloseForTests();

        string[] lines = File.ReadAllLines(Log.CurrentLogFile);
        Assert.Equal(threadCount * messagesPerThread, lines.Length);
        Assert.All(lines, line => Assert.Matches(LineRegex, line));
    }

    [Fact]
    public void MinLevel_UnterdrueckteZeilenLandenNichtImProtokoll()
    {
        Log.MinLevel = LogLevel.Warn;

        Log.Debug("sollte nicht erscheinen");
        Log.Info("sollte auch nicht erscheinen");
        Log.Warn("sollte erscheinen");
        Log.CloseForTests();

        string content = File.ReadAllText(Log.CurrentLogFile);
        Assert.DoesNotContain("sollte nicht erscheinen", content);
        Assert.DoesNotContain("sollte auch nicht erscheinen", content);
        Assert.Contains("sollte erscheinen", content);
    }

    [Fact]
    public void SchreibfehlerWerfenNie_UndSchreiberThreadLaeuftDanachWeiter()
    {
        // Ein Verzeichnisname, der bereits als Datei existiert -> CreateDirectory schlägt fehl.
        string blockedPath = Path.Combine(_tempDir, "blocked-by-file");
        File.WriteAllText(blockedPath, "ich bin eine Datei, kein Ordner");
        Log.LogsDirectory = blockedPath;

        var ex = Record.Exception(() =>
        {
            Log.Error("Fehler in unschreibbarem Verzeichnis", new InvalidOperationException("Testfehler"));
            Log.Flush(TimeSpan.FromSeconds(5));
        });
        Assert.Null(ex);

        // Schreiber-Thread muss den Fehler überlebt haben: mit gültigem Verzeichnis geht es weiter.
        string workingDir = Path.Combine(_tempDir, "works-again");
        Directory.CreateDirectory(workingDir);
        Log.LogsDirectory = workingDir;
        Log.Info("Funktioniert wieder");
        Log.CloseForTests();

        Assert.True(File.Exists(Log.CurrentLogFile));
        Assert.Contains("Funktioniert wieder", File.ReadAllText(Log.CurrentLogFile));
    }

    [Fact]
    public void Info_VerzeichnisExistiertNicht_WirdAngelegt()
    {
        string nichtVorhandenesVerzeichnis = Path.Combine(_tempDir, "noch-nicht-angelegt");
        Log.LogsDirectory = nichtVorhandenesVerzeichnis;

        var ex = Record.Exception(() =>
        {
            Log.Info("Erste Zeile in neuem Verzeichnis");
            Log.CloseForTests();
        });

        Assert.Null(ex);
        Assert.True(Directory.Exists(nichtVorhandenesVerzeichnis));
        Assert.True(File.Exists(Log.CurrentLogFile));
        Assert.Contains("Erste Zeile in neuem Verzeichnis", File.ReadAllText(Log.CurrentLogFile));
    }

    [Fact]
    public void Error_MitInnerException_BeideErscheinenImProtokoll()
    {
        var inner = new InvalidOperationException("innere Ursache");
        var outer = new IOException("äußerer Fehler", inner);

        Log.Error("Fehler mit Ursache", outer);
        Log.CloseForTests();

        string content = File.ReadAllText(Log.CurrentLogFile);
        Assert.Contains("äußerer Fehler", content);
        Assert.Contains("innere Ursache", content);
    }

    [Fact]
    public void TryRotateOversizeFile_BenenntUebergrosseDateiUm()
    {
        string path = Path.Combine(_tempDir, "mortysdlp-2026-01-01.log");
        File.WriteAllText(path, new string('x', 100));

        bool rotated = Log.TryRotateOversizeFile(path, maxBytes: 50);

        Assert.True(rotated);
        Assert.False(File.Exists(path));
        Assert.Single(Directory.GetFiles(_tempDir, "mortysdlp-2026-01-01_*.log"));
    }

    [Fact]
    public void TryRotateOversizeFile_LaesstKleineDateiUnangetastet()
    {
        string path = Path.Combine(_tempDir, "mortysdlp-2026-01-01.log");
        File.WriteAllText(path, "kurz");

        bool rotated = Log.TryRotateOversizeFile(path, maxBytes: 50);

        Assert.False(rotated);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void CleanupOldFiles_LoeschtNurDateienAelterAlsMaxAge()
    {
        string oldFile = Path.Combine(_tempDir, "mortysdlp-old.log");
        string newFile = Path.Combine(_tempDir, "mortysdlp-new.log");
        File.WriteAllText(oldFile, "alt");
        File.WriteAllText(newFile, "neu");

        var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Local);
        File.SetLastWriteTime(oldFile, now - TimeSpan.FromDays(20));
        File.SetLastWriteTime(newFile, now - TimeSpan.FromDays(1));

        Log.CleanupOldFiles(_tempDir, now, TimeSpan.FromDays(14));

        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(newFile));
    }

    [Fact]
    public void CleanupOldFiles_LiefertPfadeDerGeloeschtenDateien()
    {
        string oldFile = Path.Combine(_tempDir, "mortysdlp-old.log");
        string newFile = Path.Combine(_tempDir, "mortysdlp-new.log");
        File.WriteAllText(oldFile, "alt");
        File.WriteAllText(newFile, "neu");

        var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Local);
        File.SetLastWriteTime(oldFile, now - TimeSpan.FromDays(20));
        File.SetLastWriteTime(newFile, now - TimeSpan.FromDays(1));

        var deleted = Log.CleanupOldFiles(_tempDir, now, TimeSpan.FromDays(14));

        Assert.Equal([oldFile], deleted);
    }

    [Fact]
    public void CleanupOldFiles_NichtsZuLoeschen_LiefertLeereListe()
    {
        var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Local);

        var deleted = Log.CleanupOldFiles(_tempDir, now, TimeSpan.FromDays(14));

        Assert.Empty(deleted);
    }

    [Fact]
    public void Info_LoescheAlteDateiBeimOeffnen_ProtokolliertDieLoeschungImAktivenProtokoll()
    {
        string oldFile = Path.Combine(_tempDir, "mortysdlp-2020-01-01.log");
        File.WriteAllText(oldFile, "sehr alt");
        File.SetLastWriteTime(oldFile, DateTime.Now - TimeSpan.FromDays(30));

        // Löst WriteLine -> "neue Datei öffnen" -> CleanupOldFiles aus (kein Schreiber bislang
        // offen in dieser Testinstanz von Log.LogsDirectory).
        Log.Info("Testnachricht");
        Log.CloseForTests();

        Assert.False(File.Exists(oldFile));

        string content = File.ReadAllText(Log.CurrentLogFile);
        Assert.Contains("Alte Protokolldateien gelöscht (1)", content, StringComparison.Ordinal);
        Assert.Contains("mortysdlp-2020-01-01.log", content, StringComparison.Ordinal);
    }
}
