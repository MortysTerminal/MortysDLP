using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MortysDLP.Helpers
{
    /// <summary>Ausgang von <see cref="MarkOfTheWeb.TryRemove"/>.</summary>
    internal enum MarkOfTheWebResult
    {
        /// <summary>Die Kennzeichnung war vorhanden und wurde entfernt.</summary>
        Removed,

        /// <summary>Die Datei trug keine Kennzeichnung — nichts zu tun. Der Normalfall auf
        /// einem Dateisystem ohne alternative Datenströme (z. B. FAT32) oder wenn die
        /// aufrufende Anwendung sie beim Schreiben nie gesetzt hat.</summary>
        NotPresent,

        /// <summary>Entfernen fehlgeschlagen (z. B. fehlende Rechte). Kein Fehler des
        /// Downloads — die Datei bleibt trotzdem einsatzbereit, nur die Kennzeichnung
        /// bleibt stehen.</summary>
        Failed,
    }

    /// <summary>
    /// Entfernt den alternativen NTFS-Datenstrom <c>Zone.Identifier</c> („Mark-of-the-Web") einer
    /// Datei — die Kennzeichnung, mit der Windows aus dem Internet geladene Dateien versieht und
    /// die SmartScreen beim ersten Start ausführbarer Dateien zu einer Nachfrage oder Blockade
    /// veranlassen kann.
    ///
    /// <para>Reine Dateisystem-Operation, kein Aufruf einer Sicherheitsrichtlinie: Ob und wie
    /// streng geprüft werden muss, <b>bevor</b> diese Klasse überhaupt aufgerufen wird, entscheidet
    /// der Aufrufer (<see cref="Tools.ToolInstaller"/>) — nur dort läuft die Bedingung „bestandene
    /// Prüfsumme". Diese Klasse kennt weder Prüfsummen noch Werkzeuge, nur einen Dateipfad.</para>
    ///
    /// <para>Best-Effort und wirft nie: Ein Fehlschlag beim Entfernen ist kein Fehler der
    /// Installation, nur eine stehen gebliebene Kennzeichnung.</para>
    /// </summary>
    internal static class MarkOfTheWeb
    {
        private const int ErrorFileNotFound = 2;
        private const int ErrorPathNotFound = 3;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "DeleteFileW")]
        private static extern bool DeleteFileNative(string lpFileName);

        /// <summary>
        /// Löscht den alternativen Datenstrom <c>&lt;path&gt;:Zone.Identifier</c>. Ein
        /// P/Invoke auf <c>DeleteFileW</c> genügt dafür — ein alternativer Datenstrom ist unter
        /// Windows über genau diesen Pfad adressierbar, ohne dass .NET selbst eine API dafür
        /// mitbringt.
        /// </summary>
        public static MarkOfTheWebResult TryRemove(string path)
        {
            string streamPath = path + ":Zone.Identifier";

            try
            {
                if (DeleteFileNative(streamPath))
                    return MarkOfTheWebResult.Removed;

                int error = Marshal.GetLastWin32Error();
                return error is ErrorFileNotFound or ErrorPathNotFound
                    ? MarkOfTheWebResult.NotPresent
                    : MarkOfTheWebResult.Failed;
            }
            catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
            {
                Log.Warn($"Internet-Kennzeichnung von '{path}' nicht prüfbar: {ex.Message}");
                return MarkOfTheWebResult.Failed;
            }
        }
    }
}
