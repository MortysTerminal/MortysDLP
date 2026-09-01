using MortysDLP.Helpers;
using System.IO;

namespace MortysDLP.Services
{
    /// <summary>
    /// Whisper-<b>Modelle</b> — die Werkzeugverwaltung von whisper.cpp selbst ist jetzt
    /// <see cref="Tools.WhisperTool"/>. Diese Klasse bleibt ausschließlich für
    /// <see cref="DownloadModelAsync"/> stehen: Modelle sind unveränderliche Dateien ohne Version
    /// und ohne Werkzeugcharakter (kein <c>--version</c>, kein Ersetzen) — ein eigener Fall, der
    /// einen eigenen Weg bekommt, nicht diese Abstraktion.
    /// </summary>
    internal class WhisperUpdateService
    {
        /// <summary>Lädt ein Whisper-Modell von HuggingFace herunter.</summary>
        public async Task DownloadModelAsync(string downloadUrl, string targetPath,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            string? dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tempPath = targetPath + ".download";
            try
            {
                await ToolDownloadHelper.DownloadAssetAsync(Http.Shared, downloadUrl, tempPath, progress, cancellationToken);
                if (File.Exists(targetPath))
                    File.Delete(targetPath);
                File.Move(tempPath, targetPath);
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }
    }
}
