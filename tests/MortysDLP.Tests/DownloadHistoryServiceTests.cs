using MortysDLP.Models;
using MortysDLP.Services;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Tests laufen sequenziell innerhalb dieser Klasse (xUnit-Standardverhalten), deshalb ist das
/// Umschalten von <see cref="DownloadHistoryService.HistoryPath"/> zwischen den Tests
/// unproblematisch. Jeder Test bekommt trotzdem sein eigenes Temp-Verzeichnis.
/// </summary>
public class DownloadHistoryServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _historyPath;

    public DownloadHistoryServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.History", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _historyPath = Path.Combine(_tempDir, "download_history.json");
        DownloadHistoryService.HistoryPath = _historyPath;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private static DownloadHistoryEntry MakeEntry(string url, DateTime at) =>
        new() { Url = url, Title = url, DownloadedAt = at };

    [Fact]
    public async Task LoadAsync_DateiExistiertNicht_GibtLeereListeZurueck()
    {
        var result = await DownloadHistoryService.LoadAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadAsync_LeereDatei_GibtLeereListeZurueckUndSichertDatei()
    {
        await File.WriteAllTextAsync(_historyPath, "");

        var result = await DownloadHistoryService.LoadAsync();

        Assert.Empty(result);
        Assert.False(File.Exists(_historyPath));
        Assert.Single(Directory.GetFiles(_tempDir, "download_history.corrupt-*.json"));
    }

    [Fact]
    public async Task LoadAsync_KaputtesJson_GibtLeereListeZurueckUndSichertDatei()
    {
        await File.WriteAllTextAsync(_historyPath, "{{{kaputt");

        var result = await DownloadHistoryService.LoadAsync();

        Assert.Empty(result);
        Assert.False(File.Exists(_historyPath));
        Assert.Single(Directory.GetFiles(_tempDir, "download_history.corrupt-*.json"));
    }

    [Fact]
    public async Task LoadAsync_JsonMitFalschemTyp_GibtLeereListeZurueckUndSichertDatei()
    {
        // Ein einzelnes Objekt statt eines Arrays -> Deserialisierung als List<T> schlägt fehl.
        await File.WriteAllTextAsync(_historyPath, "{\"Url\":\"https://example.com\"}");

        var result = await DownloadHistoryService.LoadAsync();

        Assert.Empty(result);
        Assert.False(File.Exists(_historyPath));
    }

    [Fact]
    public async Task LoadAsync_GesperrteDatei_WirftNichtUndGibtLeereListeZurueck()
    {
        await File.WriteAllTextAsync(_historyPath, "[]");

        using (new FileStream(_historyPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = await DownloadHistoryService.LoadAsync();
            Assert.Empty(result);
        }
    }

    [Fact]
    public async Task LoadAsync_OrdnerStattDatei_WirftNichtUndGibtLeereListeZurueck()
    {
        // File.Exists ist false für einen Ordner gleichen Namens -> kein Leseversuch.
        Directory.CreateDirectory(_historyPath);

        var result = await DownloadHistoryService.LoadAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task AddAsync_SpeichertUndLaedtEintragKorrekt()
    {
        await DownloadHistoryService.AddAsync(MakeEntry("https://example.com/video", DateTime.Now));

        var result = await DownloadHistoryService.LoadAsync();

        Assert.Single(result);
        Assert.Equal("https://example.com/video", result[0].Url);
    }

    [Fact]
    public async Task AddAsync_ZieldateiSchreibgeschuetzt_WirftNicht()
    {
        await File.WriteAllTextAsync(_historyPath, "[]");
        File.SetAttributes(_historyPath, FileAttributes.ReadOnly);
        try
        {
            var ex = await Record.ExceptionAsync(() =>
                DownloadHistoryService.AddAsync(MakeEntry("https://example.com", DateTime.Now)));

            Assert.Null(ex);
        }
        finally
        {
            File.SetAttributes(_historyPath, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task AddAsync_VerzeichnisFehlt_WirftNicht()
    {
        DownloadHistoryService.HistoryPath = Path.Combine(_tempDir, "does-not-exist", "download_history.json");

        var ex = await Record.ExceptionAsync(() =>
            DownloadHistoryService.AddAsync(MakeEntry("https://example.com", DateTime.Now)));

        Assert.Null(ex);
    }

    [Fact]
    public async Task WriteAtomicAsync_FehlerBeimVerschieben_BestehendeDateiBleibtUnveraendert()
    {
        const string original = "[{\"Url\":\"https://original\"}]";
        await File.WriteAllTextAsync(_historyPath, original);

        using (new FileStream(_historyPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var ex = await Record.ExceptionAsync(() => DownloadHistoryService.SaveAsync(
            [
                new DownloadHistoryEntry { Url = "https://neu", DownloadedAt = DateTime.Now }
            ]));

            Assert.Null(ex); // SaveAsync fängt den Fehler intern ab
        }

        string contentAfter = await File.ReadAllTextAsync(_historyPath);
        Assert.Equal(original, contentAfter);
        Assert.False(File.Exists(_historyPath + ".tmp"));
    }

    [Fact]
    public async Task AddAsync_ZwanzigGleichzeitigeAufrufe_ErzeugenGueltigesJsonMitKorrekterAnzahl()
    {
        var tasks = Enumerable.Range(0, 20)
            .Select(i => DownloadHistoryService.AddAsync(MakeEntry($"https://example.com/{i}", DateTime.Now.AddSeconds(i))))
            .ToArray();

        await Task.WhenAll(tasks);

        var result = await DownloadHistoryService.LoadAsync();
        Assert.Equal(20, result.Count);
    }

    [Fact]
    public async Task SaveAsync_MaxEntriesNullOderNegativ_BehandeltAlsMindestensEins()
    {
        short original = Properties.Settings.Default.DownloadHistoryFileMaxEntries;
        Properties.Settings.Default.DownloadHistoryFileMaxEntries = 0;
        try
        {
            await DownloadHistoryService.SaveAsync(
            [
                new DownloadHistoryEntry { Url = "https://a", DownloadedAt = DateTime.Now },
                new DownloadHistoryEntry { Url = "https://b", DownloadedAt = DateTime.Now.AddSeconds(1) },
            ]);

            var result = await DownloadHistoryService.LoadAsync();
            Assert.Single(result);
        }
        finally
        {
            Properties.Settings.Default.DownloadHistoryFileMaxEntries = original;
        }
    }

    [Fact]
    public async Task ClearAsync_LeertVorhandenenVerlauf()
    {
        await DownloadHistoryService.AddAsync(MakeEntry("https://example.com", DateTime.Now));

        await DownloadHistoryService.ClearAsync();

        var result = await DownloadHistoryService.LoadAsync();
        Assert.Empty(result);
    }

    private void MakeCorruptBackup(string suffix, DateTime lastWriteUtc)
    {
        string path = Path.Combine(_tempDir, $"download_history.corrupt-{suffix}.json");
        File.WriteAllText(path, "kaputt");
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
    }

    [Fact]
    public void PruneCorruptBackups_MehrAlsDreiSicherungen_BehaeltNurDieDreiJuengsten()
    {
        var now = DateTime.UtcNow;
        MakeCorruptBackup("1", now.AddDays(-4));
        MakeCorruptBackup("2", now.AddDays(-3));
        MakeCorruptBackup("3", now.AddDays(-2));
        MakeCorruptBackup("4", now.AddDays(-1));
        MakeCorruptBackup("5", now);

        var deleted = DownloadHistoryService.PruneCorruptBackups(_tempDir);

        Assert.Equal(2, deleted.Count);
        var remaining = Directory.GetFiles(_tempDir, "download_history.corrupt-*.json")
            .Select(Path.GetFileName).ToHashSet();
        Assert.Equal(3, remaining.Count);
        Assert.Contains("download_history.corrupt-3.json", remaining);
        Assert.Contains("download_history.corrupt-4.json", remaining);
        Assert.Contains("download_history.corrupt-5.json", remaining);
    }

    [Fact]
    public void PruneCorruptBackups_DreiOderWeniger_LoeschtNichts()
    {
        var now = DateTime.UtcNow;
        MakeCorruptBackup("1", now.AddDays(-1));
        MakeCorruptBackup("2", now);

        var deleted = DownloadHistoryService.PruneCorruptBackups(_tempDir);

        Assert.Empty(deleted);
        Assert.Equal(2, Directory.GetFiles(_tempDir, "download_history.corrupt-*.json").Length);
    }

    [Fact]
    public void PruneCorruptBackups_OrdnerFehlt_WirftNichtUndLiefertLeereListe()
    {
        string missing = Path.Combine(_tempDir, "does-not-exist");

        var deleted = DownloadHistoryService.PruneCorruptBackups(missing);

        Assert.Empty(deleted);
    }
}
