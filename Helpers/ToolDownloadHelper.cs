using System.IO;
using System.Net.Http;

namespace MortysDLP.Services
{
    internal static class ToolDownloadHelper
    {
        private const int BufferSize = 81920;

        /// <param name="progress">Fortschritt als Anteil (0.0–1.0) — siehe
        /// <c>werkstatt/02-BEST-PRACTICES.md</c>, Abschnitt 8. Bleibt unbenutzt, solange
        /// <c>Content-Length</c> fehlt (Gesamtgröße unbekannt).</param>
        public static async Task DownloadAssetAsync(HttpClient client, string url, string targetPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = total != -1 && progress != null;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

            var buffer = new byte[BufferSize];
            long totalRead = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                totalRead += read;
                if (canReportProgress)
                    // Math.Clamp: eine unzuverlässige Content-Length darf den Balken nie über
                    // 100 % treiben.
                    progress!.Report(Math.Clamp((double)totalRead / total, 0.0, 1.0));
            }
        }
    }
}