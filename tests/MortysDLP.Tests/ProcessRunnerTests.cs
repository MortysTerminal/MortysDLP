using MortysDLP.Services;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="ProcessRunner"/> gegen ein kleines PowerShell-Testskript, das seine
/// empfangenen Argumente unverändert zurückmeldet — das ist der einzige zuverlässige Weg,
/// den tatsächlichen Argv des Zielprozesses zu beobachten (cmd.exe /c parst Sonderzeichen wie
/// "&amp;" selbst um und würde das Ergebnis verfälschen).
///
/// Skript-Aufruf: <c>helper.ps1 &lt;exitCode&gt; &lt;sleepMs&gt; &lt;...echoArgs&gt;</c> —
/// gibt jedes weitere Argument als eigene Zeile "ARG:&lt;wert&gt;" aus, schläft optional und
/// beendet sich mit dem angegebenen Exit-Code.
/// </summary>
public class ProcessRunnerTests : IDisposable
{
    private readonly string _scriptPath;

    public ProcessRunnerTests()
    {
        _scriptPath = Path.Combine(Path.GetTempPath(), $"ProcessRunnerTests_{Guid.NewGuid():N}.ps1");
        File.WriteAllText(_scriptPath, """
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [string[]]$A = @()
            )
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            $exitCode = [int]$A[0]
            $sleepMs = [int]$A[1]
            for ($i = 2; $i -lt $A.Count; $i++) { Write-Output ("ARG:" + $A[$i]) }
            if ($sleepMs -gt 0) { Start-Sleep -Milliseconds $sleepMs }
            exit $exitCode
            """);
    }

    public void Dispose()
    {
        try { File.Delete(_scriptPath); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private List<string> BuildArgs(int exitCode, int sleepMs, params string[] echoArgs)
    {
        var args = new List<string>
        {
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", _scriptPath,
            exitCode.ToString(CultureInfo.InvariantCulture), sleepMs.ToString(CultureInfo.InvariantCulture),
        };
        args.AddRange(echoArgs);
        return args;
    }

    [Theory]
    [InlineData("x\" --exec \"calc.exe")]
    [InlineData("mit Leerzeichen")]
    [InlineData("amp & sign")]
    [InlineData("nur\"Anfuehrungszeichen\"")]
    public async Task RunAsync_UebergibtSonderzeichenAlsGenauEinArgument(string eingabe)
    {
        var result = await ProcessRunner.RunAsync("powershell.exe", BuildArgs(0, 0, eingabe));

        Assert.True(result.Success);
        Assert.Equal($"ARG:{eingabe}", result.StdOut.Trim());
    }

    [Fact]
    public async Task RunAsync_MehrereArgumenteKommenGetrenntAn()
    {
        var result = await ProcessRunner.RunAsync("powershell.exe", BuildArgs(0, 0, "eins", "zwei drei", "vier&fünf"));

        var lines = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(["ARG:eins", "ARG:zwei drei", "ARG:vier&fünf"], lines);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, false)]
    public async Task RunAsync_LiefertExitCodeUndSuccess(int exitCode, bool erwarteterErfolg)
    {
        var result = await ProcessRunner.RunAsync("powershell.exe", BuildArgs(exitCode, 0));

        Assert.Equal(exitCode, result.ExitCode);
        Assert.Equal(erwarteterErfolg, result.Success);
    }

    [Fact]
    public async Task RunAsync_UeberschreitetZeitlimit_WirftTimeoutException()
    {
        await Assert.ThrowsAsync<TimeoutException>(() =>
            ProcessRunner.RunAsync("powershell.exe", BuildArgs(0, 5000), timeout: TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public async Task RunAsync_ExternerAbbruch_WirftOperationCanceledException()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ProcessRunner.RunAsync("powershell.exe", BuildArgs(0, 5000), ct: cts.Token));
    }

    [Fact]
    public async Task RunAsync_UnicodeKommtUnverfaelschtAn()
    {
        const string text = "Ünïcödé τεστ 日本語";

        var result = await ProcessRunner.RunAsync("powershell.exe", BuildArgs(0, 0, text));

        Assert.Equal($"ARG:{text}", result.StdOut.Trim());
    }

    [Fact]
    public async Task RunAsync_UngueltigerPfad_Wirft()
    {
        string fakePath = Path.Combine(Path.GetTempPath(), $"does-not-exist_{Guid.NewGuid():N}.exe");

        await Assert.ThrowsAnyAsync<Exception>(() => ProcessRunner.RunAsync(fakePath, []));
    }

    [Fact]
    public async Task RunStreamingAsync_MeldetJedeZeileUeberCallback()
    {
        var received = new List<string>();

        await ProcessRunner.RunStreamingAsync(
            "powershell.exe", BuildArgs(0, 0, "a", "b", "c"),
            onStdOut: line => { lock (received) received.Add(line); });

        Assert.Equal(["ARG:a", "ARG:b", "ARG:c"], received);
    }

    [Fact]
    public async Task RunStreamingAsync_LeerlaufUeberschreitetIdleTimeout_WirftTimeoutException()
    {
        // Skript gibt sein Argument sofort aus und schläft danach lange -> Leerlauf.
        await Assert.ThrowsAsync<TimeoutException>(() =>
            ProcessRunner.RunStreamingAsync(
                "powershell.exe", BuildArgs(0, 5000, "erste-und-einzige-zeile"),
                timeout: null,
                idleTimeout: TimeSpan.FromMilliseconds(400)));
    }

    [Fact]
    public async Task RunStreamingAsync_OnStarted_LiefertLaufendenProzess()
    {
        // Der Process wird von ProcessRunner nach Abschluss verworfen – die Id also
        // innerhalb von onStarted lesen, solange der Prozess garantiert noch existiert.
        int capturedId = -1;
        bool onStartedCalled = false;

        var result = await ProcessRunner.RunStreamingAsync(
            "powershell.exe", BuildArgs(0, 0),
            onStarted: p =>
            {
                onStartedCalled = true;
                capturedId = p.Id;
            });

        Assert.True(onStartedCalled);
        Assert.True(capturedId > 0);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task RunStreamingAsync_OnStartedProzessKannExternGetoetetWerden()
    {
        Process? captured = null;

        var result = await ProcessRunner.RunStreamingAsync(
            "powershell.exe", BuildArgs(0, 5000),
            onStarted: p =>
            {
                captured = p;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(200);
                    try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
                });
            });

        Assert.NotNull(captured);
        Assert.False(result.Success); // durch Kill beendet, kein regulärer Exit-Code 0
    }
}
