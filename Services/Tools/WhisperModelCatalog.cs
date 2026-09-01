using MortysDLP.Helpers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MortysDLP.Services.Tools
{
    /// <summary>
    /// Ein Whisper-Modell: unveränderliche Datei ohne Version. Die Fragen, die für ein
    /// <see cref="IManagedTool"/> Sinn ergeben — „gibt es eine neuere Version?" — existieren hier
    /// nicht; die einzige Frage ist „ist die Datei vollständig da?" (<see cref="WhisperModelStore"/>).
    /// </summary>
    /// <param name="ExpectedSize">Erwartete Größe in Byte — <b>kein Schätzwert</b>. Am 2026-09-02
    /// gegen die echte <c>Content-Length</c> von HuggingFace geprüft (nicht angenommen): Eine
    /// falsche erwartete Größe macht ein vollständiges Modell dauerhaft „unvollständig".</param>
    /// <param name="Sha256">Prüfsumme, wo bekannt — am 2026-09-02 aus dem
    /// <c>X-Linked-ETag</c>-Kopf der HuggingFace-Weiterleitung gelesen und für das kleinste
    /// Modell (<c>tiny</c>) durch einen vollständigen Download samt <c>sha256sum</c>
    /// nachgerechnet, nicht nur übernommen. Modelle sind unveränderlich, deshalb hier fest
    /// hinterlegt statt bei jedem Download neu abgefragt.</param>
    internal sealed record WhisperModelEntry(
        string Id,
        string FileName,
        string DisplayNameDe,
        string DisplayNameEn,
        string DescriptionDe,
        string DescriptionEn,
        string DownloadUrl,
        string MirrorUrl,
        long ExpectedSize,
        string? Sha256)
    {
        public string GetDisplayName(string lang) => lang == "de" ? DisplayNameDe : DisplayNameEn;

        public string GetDescription(string lang) => lang == "de" ? DescriptionDe : DescriptionEn;

        /// <summary>Für die Größenspalte der Oberfläche — aus der echten Byte-Zahl berechnet,
        /// nicht als eigener, separat gepflegter Text (der könnte von <see cref="ExpectedSize"/>
        /// abweichen, ohne dass es auffällt).</summary>
        public string FormatSize() => WhisperModelCatalog.FormatSize(ExpectedSize);
    }

    /// <summary>
    /// Die bekannten Whisper-Modelle an einer Stelle, mit belastbaren Zahlen statt geschätzter
    /// „SizeHint"-Texte. Ersetzt <c>Models.WhisperModelInfo</c>.
    /// </summary>
    internal static class WhisperModelCatalog
    {
        private const string Owner = "ggerganov";
        private const string Repo = "whisper.cpp";

        private static string PrimaryUrl(string fileName) =>
            $"https://huggingface.co/{Owner}/{Repo}/resolve/main/{fileName}";

        /// <summary>Ausweichadresse mit identischer Pfadstruktur, wenn <c>huggingface.co</c>
        /// nicht erreichbar ist. Am 2026-09-02 geprüft: Von diesem Rechner aus antwortet
        /// <c>hf-mirror.com</c> mit einer Weiterleitung zurück auf <c>huggingface.co</c> —
        /// erwartetes Verhalten für einen Spiegel, der gezielt für Netze gedacht ist, in denen
        /// huggingface.co selbst blockiert ist (dort liefert er den Inhalt direkt). Von hier aus
        /// ließ sich das inhaltliche Ausliefern deshalb nicht Ende-zu-Ende nachweisen, nur dass
        /// die Adresse antwortet.</summary>
        private static string MirrorUrl(string fileName) =>
            $"https://hf-mirror.com/{Owner}/{Repo}/resolve/main/{fileName}";

        public static readonly IReadOnlyList<WhisperModelEntry> All =
        [
            new("tiny", "ggml-tiny.bin",
                "Tiny (~75 MB)", "Tiny (~75 MB)",
                "Sehr schnell, geringste Genauigkeit. Gut für kurze Clips oder Tests.",
                "Very fast, lowest accuracy. Good for short clips or testing.",
                PrimaryUrl("ggml-tiny.bin"), MirrorUrl("ggml-tiny.bin"),
                ExpectedSize: 77_691_713,
                Sha256: "be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21"),

            new("base", "ggml-base.bin",
                "Base (~142 MB)", "Base (~142 MB)",
                "Schnell, akzeptable Genauigkeit. Empfohlen für den Einstieg.",
                "Fast, acceptable accuracy. Recommended for getting started.",
                PrimaryUrl("ggml-base.bin"), MirrorUrl("ggml-base.bin"),
                ExpectedSize: 147_951_465,
                Sha256: "60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe"),

            new("small", "ggml-small.bin",
                "Small (~466 MB)", "Small (~466 MB)",
                "Gutes Gleichgewicht aus Geschwindigkeit und Genauigkeit.",
                "Good balance of speed and accuracy.",
                PrimaryUrl("ggml-small.bin"), MirrorUrl("ggml-small.bin"),
                ExpectedSize: 487_601_967,
                Sha256: "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b"),

            new("medium", "ggml-medium.bin",
                "Medium (~1,5 GB)", "Medium (~1.5 GB)",
                "Hohe Genauigkeit, benötigt mehr Zeit und RAM.",
                "High accuracy, requires more time and RAM.",
                PrimaryUrl("ggml-medium.bin"), MirrorUrl("ggml-medium.bin"),
                ExpectedSize: 1_533_763_059,
                Sha256: "6c14d5adee5f86394037b4e4e8b59f1673b6cee10e3cf0b11bbdbee79c156208"),

            new("large-v3-turbo", "ggml-large-v3-turbo.bin",
                "Large v3 Turbo (~1,6 GB)", "Large v3 Turbo (~1.6 GB)",
                "Sehr hohe Genauigkeit bei moderatem Ressourcenverbrauch. Empfohlen für beste Ergebnisse.",
                "Very high accuracy with moderate resource usage. Recommended for best results.",
                PrimaryUrl("ggml-large-v3-turbo.bin"), MirrorUrl("ggml-large-v3-turbo.bin"),
                ExpectedSize: 1_624_555_275,
                Sha256: "1fc70f774d38eb169993ac391eea357ef47c88757ef72ee5943879b7e8e2bc69"),

            new("large-v3", "ggml-large-v3.bin",
                "Large v3 (~3,1 GB)", "Large v3 (~3.1 GB)",
                "Höchste Genauigkeit, benötigt viel RAM und Zeit. Nur für leistungsstarke PCs.",
                "Highest accuracy, requires much RAM and time. Only for powerful PCs.",
                PrimaryUrl("ggml-large-v3.bin"), MirrorUrl("ggml-large-v3.bin"),
                ExpectedSize: 3_095_033_483,
                Sha256: "64d182b440b98d5203c4f9bd541544d84c605196c4f7b845dfa11fb23594d1e2"),
        ];

        /// <summary>Summe der tatsächlichen Dateigrößen aller vorhandenen Modelle in
        /// <paramref name="modelsDir"/> — für die Gesamtgrößen-Anzeige der Seite „Werkzeuge".
        /// Zählt bewusst auch ein unvollständiges Modell mit: Was auf der Platte liegt, belegt
        /// Platz, unabhängig davon, ob der Download je fertig wurde.</summary>
        public static long GetInstalledSize(string modelsDir)
        {
            long total = 0;

            foreach (var model in All)
            {
                try
                {
                    var info = new FileInfo(Path.Combine(modelsDir, model.FileName));
                    if (info.Exists)
                        total += info.Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Warn($"[{model.Id}] Größe nicht lesbar: {ex.Message}");
                }
            }

            return total;
        }

        /// <summary>Formatiert eine Byte-Zahl als Größenangabe (<c>KB</c>/<c>MB</c>/<c>GB</c>,
        /// dezimal). Kulturunabhängig — dieselbe Schreibweise unabhängig von der UI-Sprache, wie
        /// zuvor bei den festen „SizeHint"-Texten.</summary>
        public static string FormatSize(long bytes)
        {
            const double Kb = 1_000;
            const double Mb = 1_000 * Kb;
            const double Gb = 1_000 * Mb;

            return bytes switch
            {
                >= (long)Gb => (bytes / Gb).ToString("0.#", CultureInfo.InvariantCulture) + " GB",
                >= (long)Mb => (bytes / Mb).ToString("0", CultureInfo.InvariantCulture) + " MB",
                >= (long)Kb => (bytes / Kb).ToString("0", CultureInfo.InvariantCulture) + " KB",
                _ => bytes.ToString(CultureInfo.InvariantCulture) + " B",
            };
        }
    }
}
