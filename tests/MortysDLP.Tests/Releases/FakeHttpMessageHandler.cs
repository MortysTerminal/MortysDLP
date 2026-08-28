using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Tests.Releases;

/// <summary>
/// <see cref="HttpMessageHandler"/>-Ersatz für Tests: bildet URL-Muster auf vorgegebene
/// Antworten ab (Statuscode, Kopfzeilen, Inhalt), zählt Aufrufe je Muster und kann Verzögerung
/// oder eine Ausnahme statt einer Antwort simulieren. Wird von den Release-Quellen-Tests
/// genutzt und in W2-T04b, T05, T06, T07 und T08 wiederverwendet — siehe
/// <c>werkstatt/tasks/W2-T04a.md</c>.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<Route> _routes = [];

    /// <summary>Gesamtzahl aller gesendeten Anfragen, unabhängig vom Muster.</summary>
    public int TotalCallCount { get; private set; }

    /// <summary>Registriert eine Antwort für jede URL, die auf <paramref name="urlPattern"/>
    /// (regulärer Ausdruck) passt. Mehrfach passende Muster: das zuerst registrierte gewinnt.</summary>
    public FakeHttpMessageHandler When(
        string urlPattern,
        HttpStatusCode status = HttpStatusCode.OK,
        string? content = null,
        IReadOnlyDictionary<string, string>? headers = null,
        TimeSpan? delay = null,
        Exception? throwException = null)
    {
        _routes.Add(new Route(new Regex(urlPattern), status, content, headers, delay, throwException));
        return this;
    }

    /// <summary>Wie oft eine Anfrage genau dieses Musters gesendet wurde.</summary>
    public int CallCount(string urlPattern) =>
        _routes.FirstOrDefault(r => r.PatternText == urlPattern)?.CallCount ?? 0;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        TotalCallCount++;

        string url = request.RequestUri?.ToString() ?? string.Empty;
        var route = _routes.FirstOrDefault(r => r.Pattern.IsMatch(url))
            ?? throw new InvalidOperationException($"Kein Testmuster für '{url}' hinterlegt.");

        route.CallCount++;

        if (route.Delay is { } delay)
            await Task.Delay(delay, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (route.ThrowException is { } exception)
            throw exception;

        var response = new HttpResponseMessage(route.Status)
        {
            Content = route.Content != null
                ? new StringContent(route.Content, Encoding.UTF8)
                : new ByteArrayContent([]),
            RequestMessage = request,
        };

        if (route.Headers != null)
        {
            foreach (var (name, value) in route.Headers)
            {
                if (!response.Headers.TryAddWithoutValidation(name, value))
                    response.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return response;
    }

    private sealed class Route(
        Regex pattern,
        HttpStatusCode status,
        string? content,
        IReadOnlyDictionary<string, string>? headers,
        TimeSpan? delay,
        Exception? throwException)
    {
        public Regex Pattern { get; } = pattern;
        public string PatternText { get; } = pattern.ToString();
        public HttpStatusCode Status { get; } = status;
        public string? Content { get; } = content;
        public IReadOnlyDictionary<string, string>? Headers { get; } = headers;
        public TimeSpan? Delay { get; } = delay;
        public Exception? ThrowException { get; } = throwException;
        public int CallCount { get; set; }
    }
}
