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
        /// Lädt das ZIP-Asset herunter.
        /// </summary>
        public async Task DownloadAssetAsync(string url, string targetPath, IProgress<double>? progress = null)
        {
            await ToolDownloadHelper.DownloadAssetAsync(_httpClient, url, targetPath, progress);
        }

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
    }
}