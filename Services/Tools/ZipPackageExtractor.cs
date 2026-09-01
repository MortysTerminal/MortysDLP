using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace MortysDLP.Services.Tools
{
    /// <summary>
    /// Holt namentlich gesuchte Einträge aus einem ZIP-Paket in von MortysDLP bestimmte
    /// Zielpfade — mit denselben Grenzen gegen „Zip-Bomben" für jeden Aufrufer
    /// (<c>02-BEST-PRACTICES.md</c>, Abschnitt 9): Anzahl der Einträge, entpackte Gesamtgröße,
    /// Kompressionsverhältnis je Eintrag. Zip-Slip ist hier kein Thema, weil nicht das Archiv in
    /// ein Verzeichnis entpackt wird, sondern genau die gesuchten Einträge einzeln in einen festen
    /// Zielpfad geschrieben werden (verglichen wird <see cref="ZipArchiveEntry.Name"/>, der reine
    /// Dateiname ohne Ordnerpfad).
    ///
    /// <para>Ursprünglich Teil von <see cref="FfmpegTool"/> allein — mit TwitchDownloaderCLI kam
    /// eine zweite Stelle hinzu, die genau dieselbe Prüfung braucht, und zwei Kopien derselben
    /// Sicherheitsgrenze sind ein Fund wie <see cref="ReleaseChecksums"/> wert, bevor daraus mehr
    /// werden. whisper.cpp bleibt bewusst außen vor: Es wählt nicht namentlich gesuchte Einträge,
    /// sondern nach einem Muster über das ganze Archiv (<see cref="WhisperTool.ExtractPackage"/>) —
    /// eine andere Form, die sich hier nicht ohne Verlust hineinzwingen ließe.</para>
    /// </summary>
    internal static class ZipPackageExtractor
    {
        public const int MaxEntries = 10_000;
        public const long MaxExtractedBytes = 500L * 1024 * 1024;
        public const long MaxCompressionRatio = 100;

        /// <returns>Die Namen der Einträge, die im Archiv nicht gefunden wurden. Leer heißt: alle
        /// da und geschrieben.</returns>
        public static List<string> ExtractNamedEntries(
            string zipPath, IReadOnlyList<(string EntryName, string TargetPath)> wanted)
        {
            var missing = new List<string>();

            using var archive = ZipFile.OpenRead(zipPath);

            if (archive.Entries.Count > MaxEntries)
            {
                throw new InvalidDataException(
                    $"Das Paket enthält {archive.Entries.Count} Einträge und damit mehr als die " +
                    $"zulässigen {MaxEntries} - es wird nicht entpackt.");
            }

            long extractedBudget = MaxExtractedBytes;

            foreach (var (entryName, targetPath) in wanted)
            {
                ZipArchiveEntry? entry = null;
                foreach (var candidate in archive.Entries)
                {
                    if (string.Equals(candidate.Name, entryName, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = candidate;
                        break;
                    }
                }

                if (entry is null)
                {
                    missing.Add(entryName);
                    continue;
                }

                if (entry.Length > extractedBudget)
                {
                    throw new InvalidDataException(
                        $"'{entryName}' würde entpackt {entry.Length} Byte belegen und überschreitet " +
                        "das Gesamtlimit - das Paket wird nicht entpackt.");
                }

                if (entry.CompressedLength > 0 && entry.Length / entry.CompressedLength > MaxCompressionRatio)
                {
                    throw new InvalidDataException(
                        $"'{entryName}' hat ein Kompressionsverhältnis über {MaxCompressionRatio}:1 " +
                        "- das Paket wird nicht entpackt.");
                }

                extractedBudget -= entry.Length;
                entry.ExtractToFile(targetPath, overwrite: true);
            }

            return missing;
        }
    }
}
