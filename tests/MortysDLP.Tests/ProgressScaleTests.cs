using MortysDLP.Services;
using MortysDLP.Tests.Releases;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft die vereinheitlichte Fortschritts-Konvention (<c>IProgress&lt;double&gt;</c> meldet
/// immer einen Anteil <c>0.0</c>–<c>1.0</c>,
/// Abschnitt 8) am Beispiel von <see cref="ToolDownloadHelper.DownloadAssetAsync"/> — der von
/// allen Werkzeug-Downloads gemeinsam genutzten Stelle. Reine Logik über
/// <see cref="HttpMessageHandler"/>-Ersätze, kein echter Netzzugriff.
/// </summary>
public class ProgressScaleTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _targetPath;

    public ProgressScaleTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.ProgressScale", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _targetPath = Path.Combine(_tempDir, "download.bin");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task DownloadAssetAsync_AlleWerteImBereich_MonotonSteigend_LetzterWertIstEins()
    {
        // Mehrere Puffergrößen (81920 Byte je Lesevorgang), damit tatsächlich mehrfach
        // berichtet wird, nicht nur einmal am Ende.
        string content = new string('x', 300_000);
        var handler = new FakeHttpMessageHandler().When(".*", content: content);
        using var client = new HttpClient(handler);

        var reported = new List<double>();
        var progress = new SyncProgress<double>(reported.Add);

        await ToolDownloadHelper.DownloadAssetAsync(
            client, "https://example.invalid/x.bin", _targetPath, progress, CancellationToken.None);

        Assert.NotEmpty(reported);
        Assert.All(reported, v => Assert.InRange(v, 0.0, 1.0));
        for (int i = 1; i < reported.Count; i++)
            Assert.True(reported[i] >= reported[i - 1], "Fortschritt muss monoton steigen.");
        Assert.Equal(1.0, reported[^1]);
    }

    [Fact]
    public async Task DownloadAssetAsync_FehlendeContentLength_KeinFortschrittAberKeineAusnahme()
    {
        var content = new UnknownLengthContent(Encoding.UTF8.GetBytes(new string('y', 5000)));
        using var client = new HttpClient(new FixedResponseHandler(content));

        var reported = new List<double>();
        var progress = new SyncProgress<double>(reported.Add);

        var exception = await Record.ExceptionAsync(() =>
            ToolDownloadHelper.DownloadAssetAsync(
                client, "https://example.invalid/x.bin", _targetPath, progress, CancellationToken.None));

        Assert.Null(exception);
        Assert.True(reported.Count == 0 || reported.All(v => v == 0.0));
        Assert.True(File.Exists(_targetPath));
    }

    [Fact]
    public async Task DownloadAssetAsync_ZuKleinGemeldeteGesamtgroesse_WirdAufEinsGeklemmt()
    {
        // Content-Length lügt absichtlich (10 statt der tatsächlichen 50.000 Byte) - ohne
        // Math.Clamp würde (double)totalRead/total weit über 1.0 hinausschießen.
        byte[] bytes = Encoding.UTF8.GetBytes(new string('z', 50_000));
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentLength = 10;
        using var client = new HttpClient(new FixedResponseHandler(content));

        var reported = new List<double>();
        var progress = new SyncProgress<double>(reported.Add);

        await ToolDownloadHelper.DownloadAssetAsync(
            client, "https://example.invalid/x.bin", _targetPath, progress, CancellationToken.None);

        Assert.NotEmpty(reported);
        Assert.All(reported, v => Assert.InRange(v, 0.0, 1.0));
    }

    /// <summary>Ruft den Rückruf synchron auf - anders als <see cref="Progress{T}"/>, das über
    /// einen <see cref="SynchronizationContext"/> postet und damit in Tests ohne UI-Kontext
    /// asynchron (und außerhalb der erwarteten Reihenfolge) auslösen kann.</summary>
    private sealed class SyncProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private sealed class FixedResponseHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    /// <summary>Liefert stets <c>TryComputeLength = false</c>, damit
    /// <c>Headers.ContentLength</c> auch nach dem Lesen <c>null</c> bleibt - echte,
    /// unbekannte Gesamtgröße statt eines bloß fehlenden Headers.</summary>
    private sealed class UnknownLengthContent(byte[] data) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            await stream.WriteAsync(data);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
