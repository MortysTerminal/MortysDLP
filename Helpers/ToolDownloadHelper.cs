using System.IO;
using System.Net.Http;

namespace MortysDLP.Services
{
    internal static class ToolDownloadHelper
    {
        private const int BufferSize = 81920;

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
                    progress!.Report((double)totalRead / total);
            }
        }
    }
}