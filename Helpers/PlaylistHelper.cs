using System.Diagnostics;
using System.Text;

namespace MortysDLP.Helpers
{
    /// <summary>
    /// Erkennt YouTube-Playlist-URLs und extrahiert Video-IDs per yt-dlp --flat-playlist.
    /// </summary>
    internal static class PlaylistHelper
    {
        /// <summary>Prüft, ob die URL einen Playlist-Parameter enthält (z.B. list=...).</summary>
        public static bool IsPlaylistUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            try
            {
                // YouTube: ?list=... oder &list=...
                if (url.Contains("list=", StringComparison.OrdinalIgnoreCase))
                    return true;

                // YouTube Music: /playlist?list=
                if (url.Contains("/playlist", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }

            return false;
        }

        /// <summary>Prüft, ob die URL neben dem Playlist-Parameter auch eine einzelne Video-ID enthält.</summary>
        public static bool ContainsSingleVideoId(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            // YouTube: ?v=... oder &v=...
            if (url.Contains("v=", StringComparison.OrdinalIgnoreCase))
                return true;

            // youtu.be/VIDEO_ID?list=...
            if (url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>Entfernt den Playlist-Parameter aus der URL, sodass nur das einzelne Video übrig bleibt.</summary>
        public static string ExtractSingleVideoUrl(string url)
        {
            try
            {
                var uri = new Uri(url);

                // youtu.be/VIDEO_ID?list=... -> youtu.be/VIDEO_ID
                if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
                {
                    return $"https://youtu.be{uri.AbsolutePath}";
                }

                // youtube.com/watch?v=xxx&list=yyy -> youtube.com/watch?v=xxx
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var videoId = query["v"];
                if (!string.IsNullOrEmpty(videoId))
                {
                    return $"https://www.youtube.com/watch?v={videoId}";
                }
            }
            catch { }

            return url;
        }

        /// <summary>
        /// Ruft per yt-dlp --flat-playlist alle Video-IDs der Playlist ab.
        /// Das ist schnell, weil keine Audio-/Video-Metadaten geladen werden.
        /// </summary>
        public static async Task<List<string>> GetPlaylistVideoIdsAsync(string ytDlpPath, string playlistUrl, CancellationToken token)
        {
            var ids = new List<string>();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ytDlpPath,
                    Arguments = $"--no-check-certificates --flat-playlist --print id \"{playlistUrl}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                }
            };

            process.Start();
            await using var reg = token.Register(() => { try { process.Kill(true); } catch { } });

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(CancellationToken.None);

            if (token.IsCancellationRequested)
                throw new OperationCanceledException(token);

            foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    ids.Add(line);
            }

            return ids;
        }

        /// <summary>Baut aus einer Video-ID eine vollständige YouTube-URL.</summary>
        public static string BuildVideoUrl(string videoId)
            => $"https://www.youtube.com/watch?v={videoId}";
    }
}
