using MortysDLP.Helpers;
using System.Net;
using System.Net.Http;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft die Wiederholstrategie (<see cref="RetryPolicy"/>) und die Ausführung
/// (<see cref="Http.SendWithRetryAsync"/>) — reine Logik bzw. mit einem
/// <see cref="HttpMessageHandler"/>-Ersatz, ohne echten Netzzugriff.
/// </summary>
public class HttpRetryTests
{
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, true)]  // 500
    [InlineData(HttpStatusCode.BadGateway, true)]            // 502
    [InlineData(HttpStatusCode.TooManyRequests, true)]       // 429
    [InlineData(HttpStatusCode.NotFound, false)]             // 404
    [InlineData(HttpStatusCode.Forbidden, false)]            // 403
    [InlineData(HttpStatusCode.Unauthorized, false)]         // 401
    [InlineData(HttpStatusCode.BadRequest, false)]           // 400
    public void ShouldRetry_NachStatuscode_WieSpezifiziert(HttpStatusCode status, bool expected)
    {
        Assert.Equal(expected, RetryPolicy.ShouldRetry(status, null));
    }

    [Fact]
    public void ShouldRetry_HttpRequestException_WirdWiederholt()
    {
        Assert.True(RetryPolicy.ShouldRetry(null, new HttpRequestException("Netzfehler")));
    }

    [Fact]
    public void ShouldRetry_TaskCanceledException_Zeitueberschreitung_WirdWiederholt()
    {
        Assert.True(RetryPolicy.ShouldRetry(null, new TaskCanceledException("Zeitüberschreitung")));
    }

    [Fact]
    public void ShouldRetry_OperationCanceledException_AusgeloestesToken_WirdNichtWiederholt()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        OperationCanceledException? captured = null;
        try { cts.Token.ThrowIfCancellationRequested(); }
        catch (OperationCanceledException ex) { captured = ex; }

        Assert.NotNull(captured);
        Assert.False(RetryPolicy.ShouldRetry(null, captured));
    }

    [Fact]
    public void ShouldRetry_UnbekannteAusnahme_WirdNichtWiederholt()
    {
        Assert.False(RetryPolicy.ShouldRetry(null, new InvalidOperationException("egal")));
    }

    [Fact]
    public void ShouldRetry_WederStatusNochAusnahme_WirdNichtWiederholt()
    {
        Assert.False(RetryPolicy.ShouldRetry(null, null));
    }

    [Theory]
    [InlineData(1, 0.0, 0.7)]
    [InlineData(1, 1.0, 1.3)]
    [InlineData(2, 0.0, 1.4)]
    [InlineData(2, 1.0, 2.6)]
    [InlineData(3, 0.0, 2.8)]
    [InlineData(3, 1.0, 5.2)]
    public void Delay_Grundwerte_BleibenImZufallsband(int attempt, double jitter, double expectedSeconds)
    {
        TimeSpan delay = RetryPolicy.Delay(attempt, jitter);

        Assert.Equal(expectedSeconds, delay.TotalSeconds, precision: 3);
    }

    [Fact]
    public void Delay_IstNieNegativ()
    {
        for (int attempt = 1; attempt <= 6; attempt++)
        {
            Assert.True(RetryPolicy.Delay(attempt, 0.0).TotalSeconds >= 0);
            Assert.True(RetryPolicy.Delay(attempt, 1.0).TotalSeconds >= 0);
        }
    }

    [Fact]
    public async Task SendWithRetryAsync_ZweiServerfehlerDannErfolg_GenauDreiVersucheUndErfolg()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.OK);
        using var client = new HttpClient(handler);

        using var response = await Http.SendWithRetryAsync(
            client, () => new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/test"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task SendWithRetryAsync_404_GenauEinVersuch()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.NotFound);
        using var client = new HttpClient(handler);

        using var response = await Http.SendWithRetryAsync(
            client, () => new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/test"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendWithRetryAsync_DreiServerfehler_LiefertLetztenFehlerhaftenResponse()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError);
        using var client = new HttpClient(handler);

        using var response = await Http.SendWithRetryAsync(
            client, () => new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/test"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task SendWithRetryAsync_AbbruchWaehrendWartezeit_WirftOperationCanceledExceptionUndWiederholtNicht()
    {
        // Erster Versuch liefert einen wiederholbaren Fehler (500), der Nutzer bricht aber
        // während der Backoff-Wartezeit ab - das darf nie zu einem zweiten Versuch führen.
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Http.SendWithRetryAsync(
                client, () => new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/test"), ct: cts.Token));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public void CreateGitHubApiRequest_SetztAcceptHeaderProAnfrage()
    {
        var request = Http.CreateGitHubApiRequest("https://api.github.com/repos/x/y/releases/latest");

        Assert.Contains(request.Headers.Accept, h => h.MediaType == "application/vnd.github+json");
    }

    [Fact]
    public void CreateGitHubApiRequest_OhneUmgebungsvariable_KeinAuthorizationHeader()
    {
        string? previous = Environment.GetEnvironmentVariable("MORTYSDLP_GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("MORTYSDLP_GITHUB_TOKEN", null);

            var request = Http.CreateGitHubApiRequest("https://api.github.com/x");

            Assert.Null(request.Headers.Authorization);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MORTYSDLP_GITHUB_TOKEN", previous);
        }
    }

    [Fact]
    public void CreateGitHubApiRequest_MitUmgebungsvariable_SetztBearerToken()
    {
        string? previous = Environment.GetEnvironmentVariable("MORTYSDLP_GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("MORTYSDLP_GITHUB_TOKEN", "test-token-123");

            var request = Http.CreateGitHubApiRequest("https://api.github.com/x");

            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-token-123", request.Headers.Authorization?.Parameter);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MORTYSDLP_GITHUB_TOKEN", previous);
        }
    }

    /// <summary>Liefert eine vorgegebene Folge von Statuscodes, unabhängig vom Anfrageinhalt -
    /// zählt mit, wie oft tatsächlich gesendet wurde.</summary>
    private sealed class StubHttpMessageHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses = new(statuses);

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();

            var status = _statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.InternalServerError;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
