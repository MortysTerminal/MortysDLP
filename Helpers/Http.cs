using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Helpers
{
    /// <summary>
    /// Ein gemeinsamer <see cref="HttpClient"/> für die gesamte Anwendung, mit
    /// Wiederholstrategie und GitHub-Kontingent-Auswertung. Ersetzt die bisher fünf
    /// getrennten <see cref="HttpClient"/>-Instanzen in den Update-Services — siehe
    /// <c>werkstatt/04-UPDATE-ARCHITEKTUR.md</c>, Abschnitt 5.
    /// </summary>
    internal static class Http
    {
        private const string GitHubTokenEnvVar = "MORTYSDLP_GITHUB_TOKEN";

        /// <summary>Für alle normalen Anfragen. Folgt Weiterleitungen.</summary>
        public static HttpClient Shared { get; } = Create(followRedirects: true);

        /// <summary>Folgt Weiterleitungen NICHT — für Quellen, die das Ziel der Weiterleitung
        /// selbst auswerten (siehe W2-T04a).</summary>
        public static HttpClient NoRedirect { get; } = Create(followRedirects: false);

        private static HttpClient Create(bool followRedirects)
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = followRedirects,
                MaxAutomaticRedirections = 5,
                ConnectTimeout = TimeSpan.FromSeconds(8),
            };

            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"MortysDLP/{AppInfo.Current ?? "unbekannt"}");
            return client;
        }

        /// <summary>
        /// Baut eine GET-Anfrage an die GitHub-API mit dem GitHub-spezifischen Accept-Header
        /// und, falls die Umgebungsvariable <c>MORTYSDLP_GITHUB_TOKEN</c> gesetzt ist, einem
        /// Entwickler-Token. Bewusst pro Anfrage statt global am Client — sonst ginge der
        /// Header (und ein etwaiges Token) auch an PyPI, HuggingFace oder beim Herunterladen
        /// von Binärdateien mit. Das Token wird nirgends protokolliert.
        /// </summary>
        public static HttpRequestMessage CreateGitHubApiRequest(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            string? token = Environment.GetEnvironmentVariable(GitHubTokenEnvVar);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return request;
        }

        /// <summary>
        /// Sendet eine Anfrage mit Wiederholstrategie (siehe <see cref="RetryPolicy"/>).
        /// <paramref name="requestFactory"/> ist bewusst eine Fabrik statt einer fertigen
        /// Instanz: Ein <see cref="HttpRequestMessage"/> darf nur einmal gesendet werden, ein
        /// zweiter Versuch mit demselben Objekt wirft. Ein Abbruch über
        /// <paramref name="ct"/> wird NIE wiederholt — nur so lässt sich ein Download
        /// jederzeit abbrechen.
        /// </summary>
        public static async Task<HttpResponseMessage> SendWithRetryAsync(
            HttpClient client,
            Func<HttpRequestMessage> requestFactory,
            HttpCompletionOption completion = HttpCompletionOption.ResponseHeadersRead,
            CancellationToken ct = default)
        {
            HttpResponseMessage? response = null;
            Exception? lastException = null;
            string host = "?";

            for (int attempt = 1; attempt <= RetryPolicy.MaxAttempts; attempt++)
            {
                response?.Dispose();
                response = null;

                using var request = requestFactory();
                host = request.RequestUri?.Host ?? "?";

                try
                {
                    response = await client.SendAsync(request, completion, ct);
                    lastException = null;
                }
                catch (Exception ex)
                {
                    // Ein Abbruch über das eigene Token ist niemals ein Wiederholgrund - das
                    // unterscheidet ihn von einer internen Zeitüberschreitung, die dieselbe
                    // Exception-Form annehmen kann. RetryPolicy.ShouldRetry kennt ct nicht,
                    // deshalb die Prüfung hier zuerst.
                    if (ct.IsCancellationRequested)
                        throw;

                    if (!RetryPolicy.ShouldRetry(null, ex))
                        throw;

                    lastException = ex;
                    Log.Warn($"Anfrage an {host} fehlgeschlagen (Versuch {attempt}/{RetryPolicy.MaxAttempts})", ex);
                }

                if (response != null)
                {
                    if (response.IsSuccessStatusCode || !RetryPolicy.ShouldRetry(response.StatusCode, null))
                        return response;

                    Log.Warn($"Anfrage an {host} lieferte Status {(int)response.StatusCode} " +
                        $"(Versuch {attempt}/{RetryPolicy.MaxAttempts})");
                }

                if (attempt == RetryPolicy.MaxAttempts)
                    break;

                await Task.Delay(RetryPolicy.Delay(attempt, Random.Shared.NextDouble()), ct);
            }

            if (response != null)
            {
                Log.Warn($"Anfrage an {host} endgültig fehlgeschlagen (Status {(int)response.StatusCode})");
                return response;
            }

            Log.Warn($"Anfrage an {host} endgültig fehlgeschlagen", lastException);
            throw lastException ?? new HttpRequestException("Anfrage fehlgeschlagen.");
        }
    }

    /// <summary>
    /// Entscheidet, ob ein fehlgeschlagener Versuch wiederholt wird, und wie lange gewartet
    /// wird — getrennt von <see cref="Http.SendWithRetryAsync"/>, damit die Entscheidung ohne
    /// echte Netzaufrufe testbar ist.
    /// </summary>
    internal static class RetryPolicy
    {
        public const int MaxAttempts = 3;

        /// <summary>Wird wiederholt bei Netzfehler, Zeitüberschreitung, 5xx, 429. Nicht
        /// wiederholt bei 400, 401, 403, 404 und bei Abbruch durch den Nutzer.</summary>
        public static bool ShouldRetry(HttpStatusCode? status, Exception? ex)
        {
            if (ex != null)
            {
                // Reihenfolge wichtig: TaskCanceledException erbt von OperationCanceledException.
                if (ex is TaskCanceledException)
                    return true; // Zeitüberschreitung

                if (ex is OperationCanceledException)
                    return false; // Nutzerabbruch

                return ex is HttpRequestException; // Netzfehler
            }

            if (status is null)
                return false;

            int code = (int)status.Value;
            return code == 429 || (code >= 500 && code < 600);
        }

        /// <summary>1 s, 2 s, 4 s Grundwert je Versuch, ±30 % Zufallsanteil.
        /// <paramref name="jitter"/> liegt in [0, 1) (0 = -30 %, 1 = +30 %).</summary>
        public static TimeSpan Delay(int attempt, double jitter)
        {
            double baseSeconds = Math.Pow(2, attempt - 1);
            double factor = 1.0 + (Math.Clamp(jitter, 0.0, 1.0) * 2.0 - 1.0) * 0.3;
            return TimeSpan.FromSeconds(Math.Max(0, baseSeconds * factor));
        }
    }

    /// <summary>
    /// Wertet die Kontingent-Kopfzeilen der GitHub-API aus. <paramref name="now"/> wird
    /// überall übergeben statt intern <see cref="DateTimeOffset.UtcNow"/> zu lesen, damit sich
    /// das Ablaufen ohne echtes Warten testen lässt.
    /// </summary>
    internal static class GitHubRateLimit
    {
        private const int LowRemainingThreshold = 5;
        private static readonly TimeSpan MaxSkipDuration = TimeSpan.FromHours(1);

        public static DateTimeOffset? SkipUntilUtc { get; private set; }

        /// <summary>Liest <c>X-RateLimit-Remaining</c>/<c>-Reset</c> aus einer Antwort. Bei
        /// einem Rest unter 5 gilt das Kontingent bis zum Reset-Zeitpunkt (höchstens aber eine
        /// Stunde) als erschöpft. Fehlende Kopfzeilen ändern nichts.</summary>
        public static void Observe(HttpResponseHeaders headers, DateTimeOffset now)
        {
            if (!TryGetIntHeader(headers, "X-RateLimit-Remaining", out int remaining))
                return;

            if (remaining >= LowRemainingThreshold)
                return;

            DateTimeOffset resetUtc = now + MaxSkipDuration;
            if (TryGetLongHeader(headers, "X-RateLimit-Reset", out long resetUnixSeconds))
            {
                var candidate = DateTimeOffset.FromUnixTimeSeconds(resetUnixSeconds);
                if (candidate < resetUtc)
                    resetUtc = candidate;
            }

            if (resetUtc <= now)
                return;

            SkipUntilUtc = resetUtc;
            Log.Info($"GitHub-API-Kontingent knapp (verbleibend={remaining}) - " +
                $"API-Quellen bis {resetUtc:u} übersprungen.");
        }

        public static bool IsExhausted(DateTimeOffset now) =>
            SkipUntilUtc.HasValue && now < SkipUntilUtc.Value;

        /// <summary>Nur für Tests: setzt den beobachteten Kontingent-Zustand zurück, damit
        /// Testfälle sich nicht gegenseitig beeinflussen.</summary>
        internal static void ResetForTests() => SkipUntilUtc = null;

        private static bool TryGetIntHeader(HttpResponseHeaders headers, string name, out int value)
        {
            value = 0;
            string? first = GetFirstHeaderValue(headers, name);
            return first != null && int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetLongHeader(HttpResponseHeaders headers, string name, out long value)
        {
            value = 0;
            string? first = GetFirstHeaderValue(headers, name);
            return first != null && long.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        private static string? GetFirstHeaderValue(HttpResponseHeaders headers, string name) =>
            headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
    }
}
