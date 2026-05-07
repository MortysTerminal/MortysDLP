using MortysDLP.Models;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace MortysDLP.Services
{
    /// <summary>
    /// Kapselt alle Operationen rund um Whisper:
    /// Audio-Extraktion per ffmpeg, Transkription per whisper.cpp und Kapitel-Export.
    /// </summary>
    internal static class WhisperService
    {
        // ── Pfade ───────────────────────────────────────────────────────────────────

        public static string WhisperExePath =>
            NormalizePath(Properties.Settings.Default.WhisperPath);

        public static string ModelsDirectory =>
            NormalizePath(Properties.Settings.Default.WhisperModelsDir);

        /// <summary>Normalisiert Windows-Pfade: ersetzt doppelte Backslashes durch einfache.</summary>
        private static string NormalizePath(string path) =>
            string.IsNullOrEmpty(path) ? path : System.IO.Path.GetFullPath(path);

        public static bool IsWhisperInstalled() =>
            File.Exists(WhisperExePath);

        public static bool IsModelInstalled(string modelId)
        {
            var model = WhisperModelInfo.All.FirstOrDefault(m => m.Id == modelId);
            if (model == null) return false;
            return File.Exists(Path.Combine(ModelsDirectory, model.FileName));
        }

        public static string GetModelPath(string modelId)
        {
            var model = WhisperModelInfo.All.First(m => m.Id == modelId);
            return Path.Combine(ModelsDirectory, model.FileName);
        }

        public static IEnumerable<WhisperModelInfo> GetInstalledModels()
        {
            string dir = ModelsDirectory;
            return WhisperModelInfo.All.Where(m => m.IsDownloaded(dir));
        }

        // ── Verzeichnis sicherstellen ────────────────────────────────────────────────

        public static void EnsureModelsDirExists()
        {
            if (!Directory.Exists(ModelsDirectory))
                Directory.CreateDirectory(ModelsDirectory);
        }

        // ── Deinstallation ───────────────────────────────────────────────────────────

        /// <summary>
        /// Deinstalliert Whisper.
        /// keepModels=true → löscht nur whisper.exe und alle DLLs (Modelle bleiben erhalten, Reparatur möglich).
        /// keepModels=false → löscht das gesamte Whisper-Verzeichnis rekursiv.
        /// </summary>
        public static Task UninstallAsync(bool keepModels) => Task.Run(() =>
        {
            string whisperExe = WhisperExePath;
            string? whisperDir = Path.GetDirectoryName(whisperExe);
            if (string.IsNullOrEmpty(whisperDir)) return;

            if (keepModels)
            {
                // Nur .exe und .dll Dateien entfernen, Modelle-Unterordner bleibt erhalten
                foreach (string file in Directory.GetFiles(whisperDir, "*.exe"))
                    File.Delete(file);
                foreach (string file in Directory.GetFiles(whisperDir, "*.dll"))
                    File.Delete(file);
            }
            else
            {
                // Gesamtes Verzeichnis inkl. Modelle löschen
                if (Directory.Exists(whisperDir))
                    Directory.Delete(whisperDir, recursive: true);
            }
        });

        // ── Audio-Extraktion

        /// <summary>
        /// Extrahiert den Audiotrack einer Mediendatei in eine temporäre WAV-Datei (16 kHz Mono).
        /// Whisper arbeitet intern immer mit 16 kHz Mono – das ist die optimale Auflösung.
        /// </summary>
        public static async Task<string> ExtractAudioToWavAsync(
            string ffmpegPath, string inputFile, IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string tempWav = Path.Combine(Path.GetTempPath(),
                $"whisper_audio_{Guid.NewGuid():N}.wav");

            string args = $"-y -i \"{inputFile}\" -ar 16000 -ac 1 -c:a pcm_s16le \"{tempWav}\"";

            progress?.Report($"[AUDIO] Extrahiere Audio für Whisper...");

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    progress?.Report($"[ffmpeg] {e.Data}");
            };

            process.Start();
            process.BeginErrorReadLine();

            await using var reg = cancellationToken.Register(() =>
            {
                try { process.Kill(true); } catch { }
            });

            await process.WaitForExitAsync(CancellationToken.None);
            process.WaitForExit();

            cancellationToken.ThrowIfCancellationRequested();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"ffmpeg beendete mit Exit-Code {process.ExitCode}");

            if (!File.Exists(tempWav))
                throw new FileNotFoundException("Temporäre WAV-Datei wurde nicht erstellt.", tempWav);

            return tempWav;
        }

        // ── Transkription ────────────────────────────────────────────────────────────

        /// <summary>
        /// Führt die Whisper-Transkription durch.
        /// Gibt den Pfad-Präfix zurück, unter dem whisper.cpp seine Ausgaben erzeugt hat.
        /// </summary>
        public static async Task<string> RunTranscriptionAsync(
            string whisperExe,
            string ffmpegPath,
            string inputFile,
            string modelPath,
            string language,           // "auto", "de", "en", ...
            bool outputTxt,
            bool outputSrt,
            bool outputVtt,
            string outputDir,
            string outputPrefix,       // Ausgabe-Dateiname ohne Endung
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default,
            IProgress<double>? numericProgress = null)
        {
            if (!File.Exists(whisperExe))
                throw new FileNotFoundException("Whisper-Executable nicht gefunden.", whisperExe);
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Whisper-Modell nicht gefunden.", modelPath);

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            // Audio für Whisper extrahieren (16 kHz Mono WAV)
            string? tempWav = null;
            string actualInput = inputFile;
            bool isAudio = IsAudioFile(inputFile);

            if (!isAudio)
            {
                tempWav = await ExtractAudioToWavAsync(ffmpegPath, inputFile, progress, cancellationToken);
                actualInput = tempWav;
            }

            try
            {
                string outputFilePath = Path.Combine(outputDir, outputPrefix);
                var sb = new System.Text.StringBuilder();

                sb.Append($"-m \"{modelPath}\" ");
                sb.Append($"-f \"{actualInput}\" ");

                // WICHTIG: Immer explizit -l setzen.
                // Ohne -l verwenden manche whisper.cpp-Versionen intern "en" als Standard,
                // was dazu führt, dass nicht-englische Sprachen übersetzt statt transkribiert werden.
                // Mit "-l auto" wird die Spracherkennung explizit aktiviert.
                sb.Append($"-l \"{language}\" ");

                if (outputTxt) sb.Append("-otxt ");
                if (outputSrt) sb.Append("-osrt ");
                if (outputVtt) sb.Append("-ovtt ");

                sb.Append($"-of \"{outputFilePath}\" ");

                // Threads: Umgebung ermitteln (max. 8, min. 2)
                int threads = Math.Min(8, Math.Max(2, Environment.ProcessorCount / 2));
                sb.Append($"-t {threads} ");

                // Fortschrittsausgabe aktivieren
                sb.Append("--print-progress ");

                progress?.Report($"[WHISPER] Starte Transkription: {Path.GetFileName(inputFile)}");
                progress?.Report($"[WHISPER] Modell: {Path.GetFileName(modelPath)}");
                progress?.Report($"[WHISPER] Sprache: {language}");
                progress?.Report($"[WHISPER] Args: {sb}");

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = whisperExe,
                        Arguments = sb.ToString(),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8,
                    }
                };

                process.OutputDataReceived += (_, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;
                    progress?.Report(e.Data);

                    // whisper.cpp --print-progress gibt Zeilen wie:
                    // "whisper_print_progress_callback: progress = 42%"
                    if (numericProgress != null && e.Data.Contains("progress ="))
                    {
                        var match = Regex.Match(e.Data, @"progress\s*=\s*(\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int pct))
                            numericProgress.Report(pct);
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        progress?.Report($"[stderr] {e.Data}");
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await using var reg = cancellationToken.Register(() =>
                {
                    try { process.Kill(true); } catch { }
                });

                await process.WaitForExitAsync(CancellationToken.None);
                process.WaitForExit();

                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report($"[WHISPER] Beendet mit Exit-Code: {process.ExitCode}");

                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"whisper beendete mit Exit-Code {process.ExitCode}");

                return outputFilePath;
            }
            finally
            {
                if (tempWav != null)
                {
                    try { File.Delete(tempWav); } catch { }
                }
            }
        }

        // ── Kapitel-Export ───────────────────────────────────────────────────────────

        /// <summary>
        /// Liest Kapitel-Informationen aus einer yt-dlp info.json-Datei und schreibt
        /// sie als formatierte Textdatei. Gibt true zurück wenn Kapitel gefunden wurden.
        /// </summary>
        public static async Task<bool> ExportChaptersFromInfoJsonAsync(
            string infoJsonPath, string outputChaptersFile)
        {
            if (!File.Exists(infoJsonPath)) return false;

            try
            {
                string json = await File.ReadAllTextAsync(infoJsonPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("chapters", out var chaptersEl) ||
                    chaptersEl.ValueKind != System.Text.Json.JsonValueKind.Array)
                    return false;

                var sb = new System.Text.StringBuilder();
                int idx = 1;
                foreach (var chapter in chaptersEl.EnumerateArray())
                {
                    double startTime = chapter.TryGetProperty("start_time", out var st) ? st.GetDouble() : 0;
                    double endTime   = chapter.TryGetProperty("end_time",   out var et) ? et.GetDouble() : 0;
                    string title     = chapter.TryGetProperty("title",      out var ti) ? ti.GetString() ?? "" : "";

                    string start = FormatSeconds(startTime);
                    string end   = FormatSeconds(endTime);
                    sb.AppendLine($"{idx:D2}. [{start} - {end}] {title}");
                    idx++;
                }

                if (idx == 1) return false; // keine Einträge

                await File.WriteAllTextAsync(outputChaptersFile, sb.ToString(), System.Text.Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── Hilfsmethoden ────────────────────────────────────────────────────────────

        private static string FormatSeconds(double totalSeconds)
        {
            var ts = TimeSpan.FromSeconds(totalSeconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        private static bool IsAudioFile(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".mp3" or ".wav" or ".flac" or ".aac" or ".m4a" or ".ogg" or ".opus" or ".wma";
        }
    }
}
