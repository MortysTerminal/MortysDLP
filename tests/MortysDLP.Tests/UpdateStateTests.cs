using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="UpdateState"/>: die reine Auswertung (<see cref="UpdateState.Evaluate"/>,
/// <see cref="UpdateState.IsBlocked"/>) mit übergebener Zeit, sowie Lesen/Schreiben/Löschen
/// gegen ein eigenes Temp-Verzeichnis.
/// </summary>
public class UpdateStateTests : IDisposable
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-09-01T10:15:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private readonly string _tempDir;
    private readonly string _filePath;

    public UpdateStateTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.UpdateState", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "update-state.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private static UpdateStateData MakeState(
        string from = "2026.06.01", string to = "2026.09.01", DateTimeOffset? startedUtc = null, int attempts = 1) => new()
    {
        FromVersion = from,
        ToVersion = to,
        StartedUtc = startedUtc ?? Now,
        Attempts = attempts,
    };

    // --- Evaluate ----------------------------------------------------------------------------

    [Fact]
    public void Evaluate_KeinZustand_LiefertNone()
    {
        var current = AppVersion.Parse("2026.06.01");

        Assert.Equal(UpdateOutcome.None, UpdateState.Evaluate(null, current, Now));
    }

    [Fact]
    public void Evaluate_LaufendeVersionGleichZielversion_LiefertSucceeded()
    {
        var state = MakeState();
        var current = AppVersion.Parse("2026.09.01");

        Assert.Equal(UpdateOutcome.Succeeded, UpdateState.Evaluate(state, current, Now));
    }

    [Fact]
    public void Evaluate_LaufendeVersionGleichAusgangsversion_LiefertFailed()
    {
        var state = MakeState();
        var current = AppVersion.Parse("2026.06.01");

        Assert.Equal(UpdateOutcome.Failed, UpdateState.Evaluate(state, current, Now));
    }

    [Fact]
    public void Evaluate_WederNochVersion_LiefertUnclear()
    {
        var state = MakeState();
        var current = AppVersion.Parse("2026.07.15");

        Assert.Equal(UpdateOutcome.Unclear, UpdateState.Evaluate(state, current, Now));
    }

    [Fact]
    public void Evaluate_EigeneVersionUnbekannt_LiefertUnclear()
    {
        var state = MakeState();

        Assert.Equal(UpdateOutcome.Unclear, UpdateState.Evaluate(state, null, Now));
    }

    [Fact]
    public void Evaluate_AelterAlsSiebenTage_LiefertStale()
    {
        var state = MakeState(startedUtc: Now - TimeSpan.FromDays(8));
        var current = AppVersion.Parse("2026.09.01");

        Assert.Equal(UpdateOutcome.Stale, UpdateState.Evaluate(state, current, Now));
    }

    [Fact]
    public void Evaluate_ZeitstempelAusDerZukunft_LiefertStale()
    {
        var state = MakeState(startedUtc: Now + TimeSpan.FromHours(1));
        var current = AppVersion.Parse("2026.09.01");

        Assert.Equal(UpdateOutcome.Stale, UpdateState.Evaluate(state, current, Now));
    }

    [Fact]
    public void Evaluate_GenauSiebenTageAlt_GiltNochNichtAlsStale()
    {
        var state = MakeState(startedUtc: Now - TimeSpan.FromDays(7));
        var current = AppVersion.Parse("2026.09.01");

        Assert.Equal(UpdateOutcome.Succeeded, UpdateState.Evaluate(state, current, Now));
    }

    // --- RecordAttemptAsync / ReadAsync / DeleteAsync -----------------------------------------

    [Fact]
    public async Task RecordAttemptAsync_ErsterVersuch_Attempts1()
    {
        await UpdateState.RecordAttemptAsync("2026.06.01", "2026.09.01", Now, _filePath);

        var read = await UpdateState.ReadAsync(_filePath);

        Assert.NotNull(read);
        Assert.Equal(1, read!.Attempts);
        Assert.Equal("2026.06.01", read.FromVersion);
        Assert.Equal("2026.09.01", read.ToVersion);
    }

    [Fact]
    public async Task RecordAttemptAsync_WiederholtDerselbenZielversion_ErhoehtAttempts()
    {
        await UpdateState.RecordAttemptAsync("2026.06.01", "2026.09.01", Now, _filePath);
        await UpdateState.RecordAttemptAsync("2026.06.01", "2026.09.01", Now, _filePath);

        var read = await UpdateState.ReadAsync(_filePath);

        Assert.Equal(2, read!.Attempts);
    }

    [Fact]
    public async Task RecordAttemptAsync_MitUpdaterProtokollpfad_WirdMitgespeichert()
    {
        // Der Grund eines fehlgeschlagenen Updates steht nur im Protokoll des Updaters. Ohne
        // diesen Pfad in der Zustandsdatei könnte die Fehlermeldung ihn nicht nennen.
        const string logPath = @"C:\Users\test\AppData\Local\MortysDLP\logs\updater-20260901-101500.log";

        await UpdateState.RecordAttemptAsync(
            "2026.06.01", "2026.09.01", Now, _filePath, updaterLogPath: logPath);

        var read = await UpdateState.ReadAsync(_filePath);

        Assert.Equal(logPath, read!.UpdaterLogPath);
    }

    [Fact]
    public async Task RecordAttemptAsync_OhneUpdaterProtokollpfad_LiefertNull()
    {
        await UpdateState.RecordAttemptAsync("2026.06.01", "2026.09.01", Now, _filePath);

        var read = await UpdateState.ReadAsync(_filePath);

        Assert.Null(read!.UpdaterLogPath);
    }

    [Fact]
    public async Task RecordAttemptAsync_MitChangelog_WirdMitgespeichert()
    {
        await UpdateState.RecordAttemptAsync(
            "2026.06.01", "2026.09.01", Now, _filePath, changelog: "### Neu\n- Beispiel");

        var read = await UpdateState.ReadAsync(_filePath);

        Assert.Equal("### Neu\n- Beispiel", read!.Changelog);
    }

    [Fact]
    public async Task RecordAttemptAsync_OhneChangelog_BleibtNull()
    {
        await UpdateState.RecordAttemptAsync("2026.06.01", "2026.09.01", Now, _filePath);

        var read = await UpdateState.ReadAsync(_filePath);

        Assert.Null(read!.Changelog);
    }

    [Fact]
    public async Task RecordAttemptAsync_AndereZielversion_BeginntNeuBei1()
    {
        await UpdateState.RecordAttemptAsync("2026.06.01", "2026.09.01", Now, _filePath);
        await UpdateState.RecordAttemptAsync("2026.06.01", "2026.09.01", Now, _filePath);
        await UpdateState.RecordAttemptAsync("2026.06.01", "2026.10.01", Now, _filePath);

        var read = await UpdateState.ReadAsync(_filePath);

        Assert.Equal(1, read!.Attempts);
        Assert.Equal("2026.10.01", read.ToVersion);
    }

    [Fact]
    public async Task DeleteAsync_EntferntDieDatei()
    {
        await UpdateState.RecordAttemptAsync("2026.06.01", "2026.09.01", Now, _filePath);

        await UpdateState.DeleteAsync(_filePath);

        Assert.Null(await UpdateState.ReadAsync(_filePath));
        Assert.False(File.Exists(_filePath));
    }

    [Fact]
    public async Task DeleteAsync_VorhandeneDatei_ProtokolliertErfolg()
    {
        string logDir = Path.Combine(_tempDir, "logs");
        Log.LogsDirectory = logDir;
        Log.MinLevel = LogLevel.Debug;

        await UpdateState.RecordAttemptAsync("2026.06.01", "2026.09.01", Now, _filePath);

        await UpdateState.DeleteAsync(_filePath);
        Log.CloseForTests();

        string content = File.ReadAllText(Log.CurrentLogFile);
        Assert.Contains("Update-Zustand gelöscht", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_DateiExistiertNicht_LiefertNull()
    {
        Assert.Null(await UpdateState.ReadAsync(_filePath));
    }

    [Fact]
    public async Task ReadAsync_KaputtesJson_LiefertNullOhneWurf()
    {
        await File.WriteAllTextAsync(_filePath, "{kaputtes json");

        var exception = await Record.ExceptionAsync(() => UpdateState.ReadAsync(_filePath));

        Assert.Null(exception);
        Assert.Null(await UpdateState.ReadAsync(_filePath));
    }

    [Fact]
    public async Task ReadAsync_LeereDatei_LiefertNull()
    {
        await File.WriteAllTextAsync(_filePath, "");

        Assert.Null(await UpdateState.ReadAsync(_filePath));
    }

    [Fact]
    public async Task ReadAsync_FalscheSchemaVersion_LiefertNull()
    {
        await File.WriteAllTextAsync(_filePath,
            """{"schemaVersion":2,"fromVersion":"2026.06.01","toVersion":"2026.09.01","attempts":1}""");

        Assert.Null(await UpdateState.ReadAsync(_filePath));
    }

    [Fact]
    public async Task RecordAttemptAsync_SchreibgeschuetztesVerzeichnis_WirftNicht()
    {
        // Ein Dateiname anstelle eines Verzeichnisses lässt CreateDirectory scheitern.
        string blockedPath = Path.Combine(_tempDir, "blocked-by-file");
        File.WriteAllText(blockedPath, "ich bin eine Datei, kein Ordner");
        string stateFileInBlockedDir = Path.Combine(blockedPath, "update-state.json");

        var exception = await Record.ExceptionAsync(() =>
            UpdateState.RecordAttemptAsync("2026.06.01", "2026.09.01", Now, stateFileInBlockedDir));

        Assert.Null(exception);
    }

    // --- Zusammenspiel mit UpdateDecision (Schleifenschutz) ------------------------------------

    [Fact]
    public void ShouldOffer_ZweiVersucheDerselbenZielversion_WirdVerweigert()
    {
        var current = AppVersion.Parse("2026.06.01");
        var latest = AppVersion.Parse("2026.09.01");
        var state = MakeState(attempts: 2);

        Assert.False(UpdateDecision.ShouldOffer(current, latest, null, state));
    }

    [Fact]
    public void ShouldOffer_EinVersuchDerselbenZielversion_WirdErlaubt()
    {
        var current = AppVersion.Parse("2026.06.01");
        var latest = AppVersion.Parse("2026.09.01");
        var state = MakeState(attempts: 1);

        Assert.True(UpdateDecision.ShouldOffer(current, latest, null, state));
    }

    [Fact]
    public void ShouldOffer_BlockierteVorversion_NeuereVersionWirdTrotzdemAngeboten()
    {
        var current = AppVersion.Parse("2026.06.01");
        var blockedTarget = AppVersion.Parse("2026.09.01");
        var state = new UpdateStateData
        {
            FromVersion = "2026.06.01",
            ToVersion = blockedTarget.ToString(),
            StartedUtc = Now,
            Attempts = 2,
        };

        var actuallyNewer = AppVersion.Parse("2026.10.01");

        Assert.True(UpdateDecision.ShouldOffer(current, actuallyNewer, null, state));
    }

    [Fact]
    public void IsBlocked_KeinZustand_LiefertFalse()
    {
        Assert.False(UpdateState.IsBlocked(null, AppVersion.Parse("2026.09.01")));
    }

    [Fact]
    public void IsBlocked_AndereZielversion_LiefertFalse()
    {
        var state = MakeState(to: "2026.09.01", attempts: 5);

        Assert.False(UpdateState.IsBlocked(state, AppVersion.Parse("2026.10.01")));
    }
}
