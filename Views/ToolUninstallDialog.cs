using MortysDLP.Services.Tools;
using MortysDLP.UITexte;
using System;
using System.Globalization;
using System.Windows;

namespace MortysDLP.Views
{
    /// <summary>Was der Nutzer in der Deinstallations-Rückfrage gewählt hat.</summary>
    internal enum ToolUninstallChoice
    {
        Cancel,

        /// <summary>Programm entfernen, Nutzerdaten (z. B. Sprachmodelle) behalten.</summary>
        KeepUserData,

        /// <summary>Alles entfernen.</summary>
        Full,
    }

    /// <summary>
    /// Die eine Rückfrage vor dem Deinstallieren eines Werkzeugs — für die Werkzeuge-Seite und das
    /// Whisper-Fenster gleich, damit dieselbe Aktion überall dieselben Möglichkeiten und Folgen hat.
    /// Werkzeuge mit Nutzerdaten (<see cref="IManagedTool.HasUserData"/>) bekommen die Wahl
    /// „Nur das Programm entfernen / Vollständig löschen", alle anderen eine Ja/Nein-Frage, die bei
    /// einem für den Betrieb nötigen Werkzeug die betroffenen Funktionen nennt.
    ///
    /// <para><c>Enter</c> bestätigt hier nichts Unwiderrufliches: Bei der Wahl liegt die
    /// Akzentfarbe und damit <c>Enter</c> auf „Abbrechen", bei der Ja/Nein-Frage gibt es keinen
    /// Enter-Standardknopf. <c>Esc</c> bricht immer ab.</para>
    /// </summary>
    internal static class ToolUninstallDialog
    {
        public static ToolUninstallChoice Ask(IManagedTool tool, Window? owner)
        {
            var T = UITextDictionary.Get;

            if (tool.HasUserData)
            {
                var choice = FluentMessageBox.Show(
                    T("WhisperModels.Uninstall.Message"),
                    T("WhisperModels.Uninstall.Title"),
                    MessageBoxImage.Warning,
                    owner,
                    (T("WhisperModels.Uninstall.Btn.KeepModels"), MessageBoxResult.Yes,    false),
                    (T("WhisperModels.Uninstall.Btn.Full"),       MessageBoxResult.No,     false),
                    (T("WhisperModels.Uninstall.Cancel"),         MessageBoxResult.Cancel, true));

                return choice switch
                {
                    MessageBoxResult.Yes => ToolUninstallChoice.KeepUserData,
                    MessageBoxResult.No => ToolUninstallChoice.Full,
                    _ => ToolUninstallChoice.Cancel,
                };
            }

            var result = FluentMessageBox.Show(
                BuildConfirmMessage(tool, T),
                T("ToolsPage.Uninstall.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                owner);

            return result == MessageBoxResult.Yes ? ToolUninstallChoice.Full : ToolUninstallChoice.Cancel;
        }

        /// <summary>Nennt bei einem für den Betrieb erforderlichen Werkzeug ausdrücklich, welche
        /// Funktionen danach nicht mehr verfügbar sind — die Zuordnung kommt aus
        /// <see cref="ToolFeatureMap"/>, dieselbe Quelle wie „Für: …" in der Zeile und der
        /// Hinweis im Startablauf.</summary>
        private static string BuildConfirmMessage(IManagedTool tool, Func<string, string> T)
        {
            string question = string.Format(CultureInfo.CurrentCulture, T("ToolsPage.Uninstall.Question"), tool.DisplayName);

            if (!tool.RequiredForOperation)
                return question;

            string consequence = string.Format(CultureInfo.CurrentCulture,
                T("ToolsPage.Uninstall.Consequence"),
                tool.DisplayName, ToolFeatureMap.Describe(tool.Id, T));

            return question + "\n\n" + consequence;
        }
    }
}
