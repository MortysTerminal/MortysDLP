namespace MortysDLP.Models
{
    internal class WhisperModelInfo
    {
        public string Id { get; }
        public string FileName { get; }
        public string DisplayNameDe { get; }
        public string DisplayNameEn { get; }
        public string DescriptionDe { get; }
        public string DescriptionEn { get; }
        public string DownloadUrl { get; }
        public string SizeHint { get; }

        public WhisperModelInfo(string id, string fileName, string displayNameDe, string displayNameEn,
            string descriptionDe, string descriptionEn, string downloadUrl, string sizeHint)
        {
            Id = id;
            FileName = fileName;
            DisplayNameDe = displayNameDe;
            DisplayNameEn = displayNameEn;
            DescriptionDe = descriptionDe;
            DescriptionEn = descriptionEn;
            DownloadUrl = downloadUrl;
            SizeHint = sizeHint;
        }

        public bool IsDownloaded(string modelsDir)
        {
            string path = System.IO.Path.Combine(modelsDir, FileName);
            return System.IO.File.Exists(path);
        }

        public string GetDisplayName(string lang) =>
            lang == "de" ? DisplayNameDe : DisplayNameEn;

        public string GetDescription(string lang) =>
            lang == "de" ? DescriptionDe : DescriptionEn;

        /// <summary>Liste aller offiziell unterstützten Whisper-Modelle mit Download-URLs.</summary>
        public static readonly IReadOnlyList<WhisperModelInfo> All = new List<WhisperModelInfo>
        {
            new("tiny",   "ggml-tiny.bin",
                "Tiny (~75 MB)",   "Tiny (~75 MB)",
                "Sehr schnell, geringste Genauigkeit. Gut für kurze Clips oder Tests.",
                "Very fast, lowest accuracy. Good for short clips or testing.",
                "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
                "~75 MB"),

            new("base",   "ggml-base.bin",
                "Base (~142 MB)",  "Base (~142 MB)",
                "Schnell, akzeptable Genauigkeit. Empfohlen für den Einstieg.",
                "Fast, acceptable accuracy. Recommended for getting started.",
                "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
                "~142 MB"),

            new("small",  "ggml-small.bin",
                "Small (~466 MB)", "Small (~466 MB)",
                "Gutes Gleichgewicht aus Geschwindigkeit und Genauigkeit.",
                "Good balance of speed and accuracy.",
                "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
                "~466 MB"),

            new("medium", "ggml-medium.bin",
                "Medium (~1,5 GB)", "Medium (~1.5 GB)",
                "Hohe Genauigkeit, benötigt mehr Zeit und RAM.",
                "High accuracy, requires more time and RAM.",
                "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin",
                "~1.5 GB"),

            new("large-v3-turbo", "ggml-large-v3-turbo.bin",
                "Large v3 Turbo (~1,6 GB)", "Large v3 Turbo (~1.6 GB)",
                "Sehr hohe Genauigkeit bei moderatem Ressourcenverbrauch. Empfohlen für beste Ergebnisse.",
                "Very high accuracy with moderate resource usage. Recommended for best results.",
                "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo.bin",
                "~1.6 GB"),

            new("large-v3", "ggml-large-v3.bin",
                "Large v3 (~3,1 GB)", "Large v3 (~3.1 GB)",
                "Höchste Genauigkeit, benötigt viel RAM und Zeit. Nur für leistungsstarke PCs.",
                "Highest accuracy, requires much RAM and time. Only for powerful PCs.",
                "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin",
                "~3.1 GB"),
        };
    }
}
