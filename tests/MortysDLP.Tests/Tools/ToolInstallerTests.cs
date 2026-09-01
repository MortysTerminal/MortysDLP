using MortysDLP.Helpers;
using MortysDLP.Services.Tools;
using System.IO;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft die <c>.old</c>-Rückfallebene und die Erfolgskontrolle gegen ein eigenes
/// Temp-Verzeichnis. Die Erfolgskontrolle kommt als Delegat herein — deshalb braucht dieser Test
/// kein echtes Werkzeug und keinen Prozessstart.
///
/// <para>Das Protokoll wird bewusst in ein Temp-Verzeichnis umgebogen (siehe die anderen
/// Testklassen, die Produktionslogik mit <c>Log</c>-Aufrufen ausführen) — und danach ausgelesen,
/// weil „auch die Erfolgsfälle stehen im Protokoll" hier ein Akzeptanzkriterium ist und keine
/// Nebensache.</para>
/// </summary>
public class ToolInstallerTests : IDisposable
{
    private const string OldContent = "alt";
    private const string NewContent = "neu";

    private readonly string _tempDir;
    private readonly string _tempLogDir;
    private readonly string _target;
    private readonly string _staged;

    public ToolInstallerTests()
    {
        string root = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.ToolInstaller", Guid.NewGuid().ToString("N"));
        _tempDir = Path.Combine(root, "tools");
        _tempLogDir = Path.Combine(root, "logs");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_tempLogDir);

        Log.LogsDirectory = _tempLogDir;
        Log.MinLevel = LogLevel.Debug;

        _target = Path.Combine(_tempDir, "werkzeug.exe");
        _staged = _target + ToolInstaller.StagedSuffix;
    }

    public void Dispose()
    {
        Log.CloseForTests();
        try { Directory.Delete(Path.GetDirectoryName(_tempDir)!, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private static Task<bool> Passes(CancellationToken ct) => Task.FromResult(true);

    private static Task<bool> Fails(CancellationToken ct) => Task.FromResult(false);

    private string BackupPath => _target + ToolInstaller.BackupSuffix;

    private void WriteStaged(string content = NewContent) => File.WriteAllText(_staged, content);

    private void WriteExisting(string content = OldContent) => File.WriteAllText(_target, content);

    private Task<ToolReplaceResult> ReplaceAsync(
        Func<CancellationToken, Task<bool>> verify, bool checksumVerified = false) =>
        ToolInstaller.ReplaceAllAsync(
            "test", [new ToolInstaller.Replacement(_target, _staged)], checksumVerified, verify,
            CancellationToken.None);

    [Fact]
    public async Task Neuinstallation_BestandeneKontrolle_DateiIstDaUndKeinRestBleibt()
    {
        WriteStaged();

        var result = await ReplaceAsync(Passes);

        Assert.True(result.Success);
        Assert.False(result.RolledBack);
        Assert.Equal(NewContent, File.ReadAllText(_target));
        Assert.False(File.Exists(_staged));
        Assert.False(File.Exists(BackupPath));
    }

    [Fact]
    public async Task Ersetzen_BestandeneKontrolle_SicherungWirdGeloescht()
    {
        WriteExisting();
        WriteStaged();

        var result = await ReplaceAsync(Passes);

        Assert.True(result.Success);
        Assert.Equal(NewContent, File.ReadAllText(_target));
        Assert.False(File.Exists(BackupPath));
        Assert.False(File.Exists(_staged));
    }

    [Fact]
    public async Task Ersetzen_KontrolleFaelltDurch_VorherigeDateiKommtZurueck()
    {
        WriteExisting();
        WriteStaged();

        var result = await ReplaceAsync(Fails);

        Assert.False(result.Success);
        Assert.True(result.RolledBack);
        Assert.Equal(OldContent, File.ReadAllText(_target));
        Assert.False(File.Exists(BackupPath));
    }

    [Fact]
    public async Task Ersetzen_KontrolleWirft_GiltWieDurchgefallen()
    {
        WriteExisting();
        WriteStaged();

        var result = await ReplaceAsync(_ => throw new InvalidOperationException("Werkzeug antwortet nicht"));

        Assert.False(result.Success);
        Assert.True(result.RolledBack);
        Assert.Equal(OldContent, File.ReadAllText(_target));
    }

    [Fact]
    public async Task Neuinstallation_KontrolleFaelltDurch_NeueDateiWirdEntfernt()
    {
        WriteStaged();

        var result = await ReplaceAsync(Fails);

        Assert.False(result.Success);
        // Zurückgeholt werden konnte nichts, weil es keinen vorherigen Stand gab - genau das
        // unterscheidet den Fall für den Aufrufer.
        Assert.False(result.RolledBack);
        Assert.False(File.Exists(_target));
        Assert.False(File.Exists(BackupPath));
    }

    [Fact]
    public async Task LiegenGebliebeneSicherung_WirdVorDemErsetzenEntfernt()
    {
        WriteExisting();
        WriteStaged();
        File.WriteAllText(BackupPath, "rest aus einem frueheren fehlschlag");

        var result = await ReplaceAsync(Passes);

        Assert.True(result.Success);
        Assert.Equal(NewContent, File.ReadAllText(_target));
        Assert.False(File.Exists(BackupPath));
    }

    [Fact]
    public async Task FehlendeBereitgestellteDatei_WirdAbgelehntOhneEtwasAnzufassen()
    {
        WriteExisting();

        var result = await ReplaceAsync(Passes);

        Assert.False(result.Success);
        Assert.False(result.RolledBack);
        Assert.Equal(OldContent, File.ReadAllText(_target));
        Assert.False(File.Exists(BackupPath));
    }

    [Fact]
    public async Task ZweiDateien_KontrolleFaelltDurch_BeideKommenZurueck()
    {
        string secondTarget = Path.Combine(_tempDir, "zweites.exe");
        string secondStaged = secondTarget + ToolInstaller.StagedSuffix;

        WriteExisting();
        WriteStaged();
        File.WriteAllText(secondTarget, OldContent);
        File.WriteAllText(secondStaged, NewContent);

        var result = await ToolInstaller.ReplaceAllAsync(
            "test",
            [
                new ToolInstaller.Replacement(_target, _staged),
                new ToolInstaller.Replacement(secondTarget, secondStaged),
            ],
            false,
            Fails,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.RolledBack);
        Assert.Equal(OldContent, File.ReadAllText(_target));
        Assert.Equal(OldContent, File.ReadAllText(secondTarget));
        Assert.False(File.Exists(secondTarget + ToolInstaller.BackupSuffix));
    }

    [Fact]
    public async Task ZweiDateien_ZweiteFehltBereitgestellt_ErsteBleibtUnangetastet()
    {
        string secondTarget = Path.Combine(_tempDir, "zweites.exe");

        WriteExisting();
        WriteStaged();
        File.WriteAllText(secondTarget, OldContent);

        var result = await ToolInstaller.ReplaceAllAsync(
            "test",
            [
                new ToolInstaller.Replacement(_target, _staged),
                new ToolInstaller.Replacement(secondTarget, secondTarget + ToolInstaller.StagedSuffix),
            ],
            false,
            Passes,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(OldContent, File.ReadAllText(_target));
        Assert.Equal(OldContent, File.ReadAllText(secondTarget));
        Assert.True(File.Exists(_staged));
    }

    [Fact]
    public async Task Erfolgsfaelle_StehenEinzelnImProtokoll()
    {
        WriteExisting();
        WriteStaged();

        await ReplaceAsync(Passes);
        Log.CloseForTests();

        string content = File.ReadAllText(Log.CurrentLogFile);

        Assert.Contains("gesichert", content, StringComparison.Ordinal);
        Assert.Contains("neue Datei eingesetzt", content, StringComparison.Ordinal);
        Assert.Contains("Erfolgskontrolle bestanden", content, StringComparison.Ordinal);
        Assert.Contains("gelöscht", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rueckholung_StehtImProtokoll()
    {
        WriteExisting();
        WriteStaged();

        await ReplaceAsync(Fails);
        Log.CloseForTests();

        string content = File.ReadAllText(Log.CurrentLogFile);

        Assert.Contains("Erfolgskontrolle nicht bestanden", content, StringComparison.Ordinal);
        Assert.Contains("zurückgeholt", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeineDateiAngegeben_WirdAbgelehnt()
    {
        var result = await ToolInstaller.ReplaceAllAsync("test", [], false, Passes, CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Abbruch_WirdDurchgereichtUndVorherigerStandKommtZurueck()
    {
        WriteExisting();
        WriteStaged();

        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ToolInstaller.ReplaceAllAsync(
                "test",
                [new ToolInstaller.Replacement(_target, _staged)],
                false,
                _ =>
                {
                    cts.Cancel();
                    cts.Token.ThrowIfCancellationRequested();
                    return Task.FromResult(true);
                },
                cts.Token));

        Assert.Equal(OldContent, File.ReadAllText(_target));
        Assert.False(File.Exists(BackupPath));
    }

    // ── Mark-of-the-Web ─────────────────────────────────────────────────────────────
    // Läuft nur auf NTFS/ReFS - siehe MarkOfTheWebTests für die Begründung, warum hier kein
    // dynamischer Skip verwendet wird (xUnit 2).

    private bool TryTagStagedWithZoneIdentifier()
    {
        try
        {
            File.WriteAllText(_staged + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private bool TargetHasZoneIdentifier()
    {
        try
        {
            File.ReadAllText(_target + ":Zone.Identifier");
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    [Fact]
    public async Task BestandenePruefsumme_EntferntDieInternetKennzeichnung()
    {
        WriteStaged();
        if (!TryTagStagedWithZoneIdentifier())
            return;

        var result = await ReplaceAsync(Passes, checksumVerified: true);

        Assert.True(result.Success);
        Assert.False(TargetHasZoneIdentifier());
    }

    /// <summary>Der Kern dieser Aufgabe: Ohne bestandene Prüfsumme bleibt die Kennzeichnung
    /// stehen, auch wenn die Datei ansonsten erfolgreich eingesetzt wurde.</summary>
    [Fact]
    public async Task KeineBestandenePruefsumme_KennzeichnungBleibtStehen()
    {
        WriteStaged();
        if (!TryTagStagedWithZoneIdentifier())
            return;

        var result = await ReplaceAsync(Passes, checksumVerified: false);

        Assert.True(result.Success);
        Assert.True(TargetHasZoneIdentifier());
    }

    [Fact]
    public async Task BestandenePruefsummeOhneKennzeichnung_LoestKeinenFehlerAus()
    {
        WriteStaged();

        var result = await ReplaceAsync(Passes, checksumVerified: true);

        Assert.True(result.Success);
        Assert.False(TargetHasZoneIdentifier());
    }

    [Fact]
    public async Task BestandenePruefsumme_StehtAlsEigeneZeileImProtokoll()
    {
        WriteStaged();
        if (!TryTagStagedWithZoneIdentifier())
            return;

        await ReplaceAsync(Passes, checksumVerified: true);
        Log.CloseForTests();

        Assert.Contains("Internet-Kennzeichnung entfernt", File.ReadAllText(Log.CurrentLogFile), StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeineBestandenePruefsumme_StehtAlsEigeneZeileImProtokoll()
    {
        WriteStaged();

        await ReplaceAsync(Passes, checksumVerified: false);
        Log.CloseForTests();

        Assert.Contains("Internet-Kennzeichnung bleibt bestehen", File.ReadAllText(Log.CurrentLogFile), StringComparison.Ordinal);
    }
}
