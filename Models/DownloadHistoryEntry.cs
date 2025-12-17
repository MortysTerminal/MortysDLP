using System;

namespace MortysDLP.Models
{
    public class DownloadHistoryEntry
    {
        public string? Url { get; set; }
        public string? Title { get; set; }
        public string? DownloadDirectory { get; set; }
        public DateTime DownloadedAt { get; set; }

        // Neu: Metadaten zur Auswahl
        public bool IsAudioOnly { get; set; }
        public string? VideoQuality { get; set; }     // z. B. "Höchste", "1080p", "144p"
        public string? VideoFormat { get; set; }      // z. B. "mp4", "mkv"
        public string? AudioFormat { get; set; }      // z. B. "mp3", "m4a"
        public string? AudioBitrate { get; set; }     // z. B. "192k", "320k"
    }
}