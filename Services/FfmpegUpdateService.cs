using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MortysDLP.Services
{
    internal class FfmpegUpdateService
    {
        private readonly HttpClient _httpClient = new();

        public FfmpegUpdateService() { }

        /// <summary>
        /// Prüft, ob ffmpeg.exe existiert.
        /// </summary>
        public bool FfmpegExists(string ffmpegPath)
        {
            return File.Exists(ffmpegPath);
        }

        /// <summary>
        /// Prüft, ob ffprobe.exe existiert.
        /// </summary>
        public bool FfprobeExists(string ffprobePath)
        {
            return File.Exists(ffprobePath);
        }

        /// <summary>
        /// Lädt das ZIP-Asset herunter.
        /// </summary>
        public async Task DownloadAssetAsync(string url, string targetPath, IProgress<double>? progress = null)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
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