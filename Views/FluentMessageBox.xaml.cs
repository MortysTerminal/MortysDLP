using MortysDLP.UITexte;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MortysDLP
{
    public partial class FluentMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private FluentMessageBox(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            InitializeComponent();
            TitleBlock.Text  = title;
            MessageBlock.Text = message;
            ConfigureIcon(icon);
            ConfigureButtons(buttons);
        }

        private FluentMessageBox(string message, string title, MessageBoxImage icon,
            params (string Text, MessageBoxResult Result, bool Primary)[] customButtons)
        {
            InitializeComponent();
            TitleBlock.Text  = title;
            MessageBlock.Text = message;
            ConfigureIcon(icon);
            foreach (var (text, result, primary) in customButtons)
                AddButton(text, result, primary);
        }

        // ─── Icon-Konfiguration ───────────────────────────────────────────────────────

        private void ConfigureIcon(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Information:
                    IconGlyph.Text       = "\uE946";
                    IconBorder.Background = new SolidColorBrush(Color.FromArgb(35,   0, 120, 212));
                    IconGlyph.Foreground  = new SolidColorBrush(Color.FromRgb (  0, 120, 212));
                    break;

                case MessageBoxImage.Warning:
                    IconGlyph.Text       = "\uE7BA";
                    IconBorder.Background = new SolidColorBrush(Color.FromArgb(35, 230, 126,  80));
                    IconGlyph.Foreground  = new SolidColorBrush(Color.FromRgb (230, 126,  80));
                    break;

                case MessageBoxImage.Error:
                    IconGlyph.Text       = "\uEA39";
                    IconBorder.Background = new SolidColorBrush(Color.FromArgb(35, 196, 43, 28));
                    IconGlyph.Foreground  = new SolidColorBrush(Color.FromRgb (196,  43, 28));
                    break;

                case MessageBoxImage.Question:
                    IconGlyph.Text       = "\uE9CE";
                    IconBorder.Background = new SolidColorBrush(Color.FromArgb(35,   0,  95, 184));
                    IconGlyph.Foreground  = new SolidColorBrush(Color.FromRgb (  0,  95, 184));
                    break;

                default:
                    IconBorder.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        // ─── Button-Konfiguration ────────────────────────────────────────────────────

        private void ConfigureButtons(MessageBoxButton buttons)
        {
            var T = UITextDictionary.Get;

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    AddButton(T("Common.Button.OK"), MessageBoxResult.OK, primary: true);
                    break;

                case MessageBoxButton.OKCancel:
                    AddButton(T("Common.Button.Cancel"), MessageBoxResult.Cancel, primary: false);
                    AddButton(T("Common.Button.OK"),     MessageBoxResult.OK,     primary: true);
                    break;

                case MessageBoxButton.YesNo:
                    AddButton(T("Common.Button.No"),  MessageBoxResult.No,  primary: false);
                    AddButton(T("Common.Button.Yes"), MessageBoxResult.Yes, primary: true);
                    break;

                case MessageBoxButton.YesNoCancel:
                    AddButton(T("Common.Button.Cancel"), MessageBoxResult.Cancel, primary: false);
                    AddButton(T("Common.Button.No"),     MessageBoxResult.No,     primary: false);
                    AddButton(T("Common.Button.Yes"),    MessageBoxResult.Yes,    primary: true);
                    break;
            }
        }

        private void AddButton(string text, MessageBoxResult result, bool primary)
        {
            var btn = new Button
            {
                Content  = text,
                MinWidth = 80,
                Padding  = new Thickness(20, 8, 20, 8),
                Margin   = new Thickness(ButtonPanel.Children.Count > 0 ? 8 : 0, 0, 0, 0)
            };

            if (primary && TryFindResource("PrimaryButtonStyle") is Style ps)
                btn.Style = ps;

            btn.Click += (_, _) => { Result = result; DialogResult = true; };
            ButtonPanel.Children.Add(btn);
        }

        // ─── Tastatur-Unterstützung ──────────────────────────────────────────────────

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape)
            {
                Result = MessageBoxResult.Cancel;
                DialogResult = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                // Primären Button (letzter = rechts) auslösen
                if (ButtonPanel.Children.OfType<Button>().LastOrDefault() is { } primary)
                    primary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
            }
        }

        // ─── Statische API ───────────────────────────────────────────────────────────

        /// <summary>Zeigt eine Fluent-MessageBox an. Ersetzt MessageBox.Show() global.</summary>
        public static MessageBoxResult Show(
            string           message,
            string           title   = "",
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage  icon    = MessageBoxImage.None,
            Window?          owner   = null)
        {
            string resolvedTitle = string.IsNullOrWhiteSpace(title) ? ResolveDefaultTitle(icon) : title;

            return Dispatch(() =>
            {
                owner ??= FindActiveWindow();
                var box = new FluentMessageBox(message, resolvedTitle, buttons, icon);
                if (owner != null) box.Owner = owner;
                box.ShowDialog();
                return box.Result;
            });
        }

        /// <summary>Zeigt eine Fluent-MessageBox mit benutzerdefinierten Button-Texten an.</summary>
        public static MessageBoxResult Show(
            string message,
            string title,
            MessageBoxImage icon,
            Window? owner,
            params (string Text, MessageBoxResult Result, bool Primary)[] customButtons)
        {
            string resolvedTitle = string.IsNullOrWhiteSpace(title) ? ResolveDefaultTitle(icon) : title;

            return Dispatch(() =>
            {
                owner ??= FindActiveWindow();
                var box = new FluentMessageBox(message, resolvedTitle, icon, customButtons);
                if (owner != null) box.Owner = owner;
                box.ShowDialog();
                return box.Result;
            });
        }

        // ─── Hilfsmethoden ───────────────────────────────────────────────────────────

        private static T Dispatch<T>(Func<T> action)
        {
            if (Application.Current?.Dispatcher.CheckAccess() != false)
                return action();

            return Application.Current!.Dispatcher.Invoke(action);
        }

        private static Window? FindActiveWindow() =>
            Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive)
            ?? Application.Current?.MainWindow;

        private static string ResolveDefaultTitle(MessageBoxImage icon)
        {
            var T = UITextDictionary.Get;
            return icon switch
            {
                MessageBoxImage.Information => T("FluentMessageBox.Title.Information"),
                MessageBoxImage.Warning     => T("FluentMessageBox.Title.Warning"),
                MessageBoxImage.Error       => T("FluentMessageBox.Title.Error"),
                MessageBoxImage.Question    => T("FluentMessageBox.Title.Question"),
                _                           => "MortysDLP"
            };
        }
    }
}
