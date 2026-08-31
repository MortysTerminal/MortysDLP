using MortysDLP.Helpers;
using MortysDLP.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MortysDLP.Services.Tools
{
    /// <summary>Die Felder der Windows-Versionsressource einer EXE, soweit sie hier interessieren.
    /// Bewusst ein eigener Typ und nicht <see cref="FileVersionInfo"/>: Der lässt sich nicht
    /// erzeugen, und damit wäre die Beurteilung unten nicht ohne echte Dateien testbar.</summary>
    internal sealed record VersionResourceInfo(
        string? ProductName,
        string? FileDescription,
        string? CompanyName,
        string? FileVersion,
        string? ProductVersion)
    {
        /// <summary>true, wenn die Datei überhaupt keine Versionsressource trägt — dann sagt sie
        /// nichts, weder für noch gegen das erwartete Werkzeug. Genau der Fall der
        /// ffmpeg-Builds von gyan.dev.</summary>
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(ProductName) &&
            string.IsNullOrWhiteSpace(FileDescription) &&
            string.IsNullOrWhiteSpace(CompanyName) &&
            string.IsNullOrWhiteSpace(FileVersion) &&
            string.IsNullOrWhiteSpace(ProductVersion);

        /// <summary>Kurzbeschreibung für das Protokoll.</summary>
        public string Describe()
        {
            string name = FirstNonEmpty(ProductName, FileDescription) ?? "ohne Namen";
            string version = FirstNonEmpty(FileVersion, ProductVersion) ?? "ohne Version";
            return $"{name} {version}";
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return null;
        }
    }

    /// <summary>
    /// Liest Version und Identität eines Werkzeugs aus der Versionsressource seiner EXE — ohne das
    /// Programm zu starten.
    ///
    /// <para><b>Warum das der Mühe wert ist:</b> <c>yt-dlp.exe</c> ist ein PyInstaller-Bündel. Bei
    /// jedem Aufruf entpackt es sich in einen Temp-Ordner, startet einen vollständigen
    /// CPython-Interpreter und beendet sich wieder — am 2026-08-31 dreimal nachgemessen:
    /// <b>3640 / 3794 / 3702 ms</b> für <c>--version</c>. Das native <c>ffmpeg.exe</c> braucht für
    /// dieselbe Frage 51–67 ms, obwohl es mit 98 MB die deutlich größere Datei ist. Es ist also
    /// nicht die Dateigröße und nicht unser Aufruf, sondern ein kompletter Programmstart, den wir
    /// unnötig auslösen. Dieselbe Auskunft steht in der Versionsressource der Datei und kostet
    /// dort <b>rund 5 ms</b>.</para>
    ///
    /// <para><b>Der Nachweis wird dadurch nicht schwächer, sondern stärker:</b> Ein fremdes
    /// Programm muss nicht mehr gestartet werden, um es abzulehnen. Am 2026-08-31 gegen echte
    /// Dateien geprüft — <c>git.exe</c> nennt sich <c>Git</c>, <c>curl.exe</c> nennt sich
    /// <c>The curl executable</c>, <c>notepad.exe</c> nennt Windows als Produkt. Keines davon
    /// käme durch, und keines würde dafür ausgeführt.</para>
    ///
    /// <para><b>Kein Ersatz, sondern eine Abkürzung:</b> Trägt eine Datei keine oder keine
    /// brauchbare Versionsressource, bleibt es beim Prozessaufruf. Eine selbst gebaute Fassung
    /// oder eine geänderte Build-Pipeline darf nicht dazu führen, dass ein funktionierendes
    /// Werkzeug als unbrauchbar gilt.</para>
    /// </summary>
    internal static class ToolVersionResource
    {
        /// <summary>Liest die Versionsressource. <c>null</c>, wenn die Datei fehlt, leer oder nicht
        /// lesbar ist — dann ist der Prozessaufruf zuständig, nicht diese Klasse. Wirft nie.</summary>
        public static VersionResourceInfo? TryRead(string exePath)
        {
            try
            {
                var file = new FileInfo(exePath);
                if (!file.Exists || file.Length == 0)
                    return null;

                var info = FileVersionInfo.GetVersionInfo(exePath);

                var resource = new VersionResourceInfo(
                    info.ProductName,
                    info.FileDescription,
                    info.CompanyName,
                    info.FileVersion,
                    info.ProductVersion);

                return resource.IsEmpty ? null : resource;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Log.Warn($"Versionsressource von '{exePath}' nicht lesbar: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Beurteilt eine gelesene Versionsressource. Reine Logik, ohne Datei- und Prozesszugriff.
        ///
        /// <list type="bullet">
        /// <item><see cref="ToolHealth.Ok"/> — der Name passt und die Version ist brauchbar.</item>
        /// <item><see cref="ToolHealth.Foreign"/> — die Datei benennt sich selbst als ein
        /// <b>anderes</b> Programm. Das ist eine abschließende Antwort: Sie muss dafür nicht
        /// gestartet werden.</item>
        /// <item><c>null</c> — die Ressource sagt nichts Verwertbares (kein Name, oder der Name
        /// passt, aber die Version ist unbrauchbar). Dann muss gefragt werden.</item>
        /// </list>
        /// </summary>
        /// <param name="expectedNames">Namen, unter denen sich das Werkzeug ausweisen darf —
        /// verglichen gegen <c>ProductName</c> und <c>FileDescription</c>, ohne
        /// Groß-/Kleinschreibung.</param>
        /// <param name="isOwnVersion">Prüft, ob die gelesene Angabe zum Versionsschema des
        /// Werkzeugs passt — dieselbe Regel wie beim Prozessaufruf, damit beide Wege nicht
        /// auseinanderlaufen können.</param>
        public static ToolProbe? Judge(
            VersionResourceInfo resource,
            IReadOnlyList<string> expectedNames,
            Func<ToolVersion, bool> isOwnVersion)
        {
            bool hasName = !string.IsNullOrWhiteSpace(resource.ProductName) ||
                           !string.IsNullOrWhiteSpace(resource.FileDescription);

            if (!hasName)
                return null;

            if (!MatchesAny(resource, expectedNames))
                return new ToolProbe(ToolHealth.Foreign, ToolVersion.Unknown, resource.Describe());

            // FileVersion zuerst: Bei yt-dlp steht dort genau "2026.08.19", während
            // ProductVersion einen Zusatz trägt ("2026.08.19 on Python 3.10.11"). Der
            // Rückgriff auf das erste Wort von ProductVersion ist für den Fall gedacht, dass
            // FileVersion leer oder auf 0.0.0.0 stehen geblieben ist.
            foreach (string? candidate in new[] { resource.FileVersion, FirstWord(resource.ProductVersion) })
            {
                var version = ToolVersion.Parse(candidate);
                if (version.HasValue && isOwnVersion(version))
                    return new ToolProbe(ToolHealth.Ok, version, resource.Describe());
            }

            // Der Name passt, die Version nicht: Das ist kein fremdes Programm, sondern eine
            // Ressource, aus der sich die Version nicht ablesen lässt. Fragen statt ablehnen.
            return null;
        }

        private static bool MatchesAny(VersionResourceInfo resource, IReadOnlyList<string> expectedNames)
        {
            foreach (string expected in expectedNames)
            {
                if (Equals(resource.ProductName, expected) || Equals(resource.FileDescription, expected))
                    return true;
            }

            return false;

            static bool Equals(string? actual, string expected) =>
                actual is not null &&
                string.Equals(actual.Trim(), expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string? FirstWord(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string trimmed = text.Trim();
            int space = trimmed.IndexOf(' ');
            return space < 0 ? trimmed : trimmed[..space];
        }
    }
}
