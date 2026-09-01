using MortysDLP.Helpers;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Tools
{
    /// <summary>Zustand eines Whisper-Modells auf der Platte — ohne Netzzugriff ermittelt.</summary>
    internal enum WhisperModelState
    {
        /// <summary>Datei fehlt oder ist leer.</summary>
        NotPresent,

        /// <summary>Datei da, aber ihre Größe liegt außerhalb der Toleranz um
        /// <see cref="WhisperModelEntry.ExpectedSize"/> — typischerweise ein abgebrochener
        /// Download. Gilt ausdrücklich <b>nicht</b> als installiert.</summary>
        Incomplete,

        /// <summary>Datei da, Größe innerhalb der Toleranz.</summary>
        Complete,
    }

    /// <summary>
    /// Zustand ermitteln, laden, löschen — für Whisper-Modelle. Modelle sind unveränderliche
    /// Dateien ohne Version (<see cref="WhisperModelCatalog"/>); die einzige Frage ist
    /// Vollständigkeit, nicht Aktualität.
    /// </summary>
    internal static class WhisperModelStore
    {
        /// <summary>±1 % — großzügig genug für unterschiedliche Ablagen desselben Modells, eng
        /// genug, um einen bei 99 % abgebrochenen Download zu erkennen. Enger gezogen riskiert
        /// falsch-negative Meldungen bei intakten Dateien - der teurere Fehler, weil er zum
        /// grundlosen Neuladen mehrerer Gigabyte verleitet.</summary>
        public const double SizeTolerance = 0.01;

        /// <summary>Reine Funktion: kein Netzzugriff, nur <see cref="FileInfo"/>.</summary>
        public static WhisperModelState GetState(
            WhisperModelEntry model, string modelsDir, double tolerance = SizeTolerance)
        {
            var info = new FileInfo(Path.Combine(modelsDir, model.FileName));
            if (!info.Exists || info.Length == 0)
                return WhisperModelState.NotPresent;

            long allowedDeviation = (long)(model.ExpectedSize * tolerance);
            long deviation = Math.Abs(info.Length - model.ExpectedSize);

            return deviation <= allowedDeviation ? WhisperModelState.Complete : WhisperModelState.Incomplete;
        }

        /// <summary>
        /// Lädt ein Modell über <see cref="VerifiedDownload"/>: <c>.part</c>-Datei, Prüfsumme
        /// (wo bekannt) und Größenabgleich, Umbenennen erst nach bestandener Prüfung — ein
        /// Abbruch kann damit keine scheinbar fertige Datei mehr hinterlassen. Bei einem
        /// Netzwerkfehler (nicht: einer falschen Prüfsumme oder Größe — das wäre ein
        /// Datenproblem, das die Ausweichadresse nicht löst) wird einmal die Ausweichadresse
        /// versucht.
        /// </summary>
        public static async Task DownloadAsync(
            WhisperModelEntry model, string modelsDir, IProgress<double>? progress, CancellationToken ct)
        {
            Directory.CreateDirectory(modelsDir);
            string targetPath = Path.Combine(modelsDir, model.FileName);

            EnsureEnoughFreeSpace(model, modelsDir);

            try
            {
                await VerifiedDownload.ToFileAsync(
                    model.DownloadUrl, targetPath, model.Sha256, model.ExpectedSize, progress, ct);
                Log.Info($"[whisper-model:{model.Id}] geladen von huggingface.co.");
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                Log.Warn($"[whisper-model:{model.Id}] huggingface.co nicht erreichbar " +
                    $"({ex.Message}) - Ausweichadresse hf-mirror.com wird versucht.");
            }
            catch (OperationCanceledException ex)
            {
                // Kein Nutzerabbruch (ct wurde nicht ausgelöst) - ein Zeitlimit ohne Antwort.
                Log.Warn($"[whisper-model:{model.Id}] huggingface.co antwortet nicht ({ex.Message}) " +
                    "- Ausweichadresse hf-mirror.com wird versucht.");
            }

            await VerifiedDownload.ToFileAsync(
                model.MirrorUrl, targetPath, model.Sha256, model.ExpectedSize, progress, ct);
            Log.Info($"[whisper-model:{model.Id}] über die Ausweichadresse hf-mirror.com geladen.");
        }

        /// <summary>Löscht ein Modell samt einer eventuell liegen gebliebenen <c>.part</c>-Datei
        /// (z. B. nach einem harten Abbruch der Anwendung mitten im Download).</summary>
        public static void Delete(WhisperModelEntry model, string modelsDir)
        {
            string path = Path.Combine(modelsDir, model.FileName);
            TryDelete(path);
            TryDelete(path + ".part");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Log.Info($"'{Path.GetFileName(path)}' gelöscht.");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"'{Path.GetFileName(path)}' konnte nicht gelöscht werden: {ex.Message}");
            }
        }

        /// <summary>Best-Effort-Hinweis vor einem mehrere Gigabyte großen Download - kein Ersatz
        /// für die eigentliche Prüfung durch <see cref="VerifiedDownload"/>, nur ein früher,
        /// billiger Hinweis statt eines Abbruchs nach stundenlangem Laden. Scheitert die Prüfung
        /// selbst (z. B. UNC-Wurzel), wird nicht blockiert - dieselbe Zurückhaltung wie bei
        /// <see cref="InstallLocation"/>.</summary>
        private static void EnsureEnoughFreeSpace(WhisperModelEntry model, string modelsDir)
        {
            try
            {
                string? root = Path.GetPathRoot(Path.GetFullPath(modelsDir));
                if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\", StringComparison.Ordinal))
                    return;

                var drive = new DriveInfo(root);
                if (!drive.IsReady)
                    return;

                if (drive.AvailableFreeSpace < model.ExpectedSize)
                {
                    throw new IOException(
                        $"Nicht genug freier Speicherplatz für '{model.FileName}': benötigt " +
                        $"{WhisperModelCatalog.FormatSize(model.ExpectedSize)}, verfügbar " +
                        $"{WhisperModelCatalog.FormatSize(drive.AvailableFreeSpace)}.");
                }
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException)
            {
                Log.Debug($"Plattenplatz für '{modelsDir}' nicht prüfbar: {ex.Message}");
            }
        }
    }
}
