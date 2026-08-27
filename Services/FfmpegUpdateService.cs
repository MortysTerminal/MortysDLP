using MortysDLP.Helpers;
using System.IO;
using System.Threading.Tasks;

namespace MortysDLP.Services
{
    internal class FfmpegUpdateService : IDownloadableToolService
    {
        /// <summary>
        /// Lädt das ZIP-Asset herunter.
        /// </summary>
        public async Task DownloadAssetAsync(string url, string targetPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            await ToolDownloadHelper.DownloadAssetAsync(Http.Shared, url, targetPath, progress, cancellationToken);
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