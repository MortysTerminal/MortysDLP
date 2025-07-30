using System.IO;
using System.Net.Http;

namespace MortysDLP.Services
{
    internal static class ToolDownloadHelper
    {
        public static async Task DownloadAssetAsync(HttpClient client, string url, string targetPath, IProgress<double>? progress = null)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = total != -1 && progress != null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;
                if (canReportProgress)
                    progress!.Report((double)totalRead / total);
            }
        }
    }
}