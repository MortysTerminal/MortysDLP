using MortysDLP.Models;
using MortysDLP.Services;
using System.Globalization;
using System.Windows;

namespace MortysDLP
{
    /// <summary>
    /// Interaktionslogik für DownloadHistoryWindow.xaml
    /// </summary>
    public partial class DownloadHistoryWindow : Window
    {
        public DownloadHistoryWindow()
        {
            /********************************************************************/
            /*
              Sprachanpassung bei Software-Start
            */
            // Debug: Sprache erzwingen
            bool forceEnglish = Properties.Settings.Default.FORCE_ENGLISH_LANGUAGE;
            SetLanguage(forceEnglish);

            /********************************************************************/

            InitializeComponent();
            SetUITexte();
            LoadHistory();
        }

        private void SetUITexte()
        {
            this.Title = UITexte.UITexte.DownloadHistory_Title;
            this.ReuseButton.Content = UITexte.UITexte.DownloadHistory_Button_ReUse;
            this.ClearButton.Content = UITexte.UITexte.DownloadHistory_Button_Clear;
            this.EmptyText.Text = UITexte.UITexte.DownloadHistory_Label_EmptyHistory;
            this.InfoText.Text = UITexte.UITexte.DownloadHistory_Label_EmptyHistory_Info;
        }

        private void SetLanguage(bool forceEnglish)
        {
            CultureInfo culture;
            if (forceEnglish)
            {
                culture = new CultureInfo("en");
            }
            else
            {
                // Standard: Englisch, aber wenn Windows-Sprache Deutsch ist, dann Deutsch verwenden
                var windowsCulture = CultureInfo.CurrentUICulture;
                if (windowsCulture.TwoLetterISOLanguageName == "de")
                    culture = new CultureInfo("de");
                else
                    culture = new CultureInfo("en");
            }

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        private void LoadHistory()
        {
            HistoryList.ItemsSource = DownloadHistoryService.Load();
            UpdateButtonStates();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            DownloadHistoryService.Clear();
            LoadHistory();
        }

        private void Reuse_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryList.SelectedItem is DownloadHistoryEntry entry)
            {
                // Hauptfenster suchen und URL setzen
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is Hauptfenster main)
                    {
                        main.tb_URL.Text = entry.Url;
                        main.Activate();
                        break;
                    }
                }
                this.Close();
            }
        }

        private void UpdateButtonStates()
        {
            bool hasItems = HistoryList.Items.Count > 0;
            ReuseButton.IsEnabled = hasItems;
            ClearButton.IsEnabled = hasItems;
            EmptyText.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            InfoText.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
