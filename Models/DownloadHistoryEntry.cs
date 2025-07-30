using System;

namespace MortysDLP.Models
{
    public class DownloadHistoryEntry
    {
        public string? Url { get; set; }
        public string? Title { get; set; }
        public string? DownloadDirectory { get; set; }
        public DateTime DownloadedAt { get; set; }
    }
}