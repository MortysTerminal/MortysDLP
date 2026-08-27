using MortysDLP.Helpers;
using MortysDLP.UITexte;
using System.Windows;
using System.Windows.Input;

namespace MortysDLP.Views
{
    /// <summary>
    /// Fehlerdialog für unbehandelte Ausnahmen. Zeigt einen verständlichen Kurztext,
    /// ausklappbare technische Details und Wege, Hilfe zu bekommen (Protokoll, Kopieren).
    /// </summary>
    public partial class ErrorDialog : Window
    {
        private readonly string _details;

        private ErrorDialog(string message, string details, bool fatal)
        {
            InitializeComponent();

            var T = UITextDictionary.Get;
            _details = details;

            Title = T("ErrorDialog.Title");
            TitleBlock.Text = T("ErrorDialog.Header");
            MessageBlock.Text = message;
            PrivacyHintBlock.Text = T("ErrorDialog.PrivacyHint");
            DetailsExpander.Header = T("ErrorDialog.DetailsLabel");
            DetailsBox.Text = details;

            btnCopyDetails.Content = T("ErrorDialog.Button.CopyDetails");
            btnOpenLogFolder.Content = T("ErrorDialog.Button.OpenLogFolder");
            btnExit.Content = T("Common.Button.Exit");
            btnClose.Content = T("Common.Button.Close");
            btnExit.Visibility = fatal ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Zeigt den Fehlerdialog an. Kann von jedem Thread aufgerufen werden.</summary>
        /// <param name="fatal">Wenn true, wird zusätzlich ein „Beenden"-Knopf angezeigt.</param>
        public static void Show(Exception ex, string? userMessage = null, bool fatal = false, Window? owner = null)
        {
            var T = UITextDictionary.Get;
            string message = userMessage ?? T("ErrorDialog.DefaultMessage");

            Dispatch(() =>
            {
                var dlg = new ErrorDialog(message, ex.ToString(), fatal);
                owner ??= FindActiveWindow();
                if (owner != null && !ReferenceEquals(owner, dlg))
                    dlg.Owner = owner;
                dlg.ShowDialog();
            });
        }

        private void btnCopyDetails_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(_details); } catch { /* Zwischenablage evtl. gesperrt – kein Beinbruch */ }
        }

        private void btnOpenLogFolder_Click(object sender, RoutedEventArgs e) => Log.OpenLogFolder();

        private void btnExit_Click(object sender, RoutedEventArgs e) => Environment.Exit(1);

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private static void Dispatch(Action action)
        {
            if (Application.Current?.Dispatcher.CheckAccess() != false)
            {
                action();
                return;
            }

            Application.Current!.Dispatcher.Invoke(action);
        }

        private static Window? FindActiveWindow() =>
            Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive)
            ?? Application.Current?.MainWindow;
    }
}
