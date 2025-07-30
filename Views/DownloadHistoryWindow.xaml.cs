using MortysDLP.Models;
using MortysDLP.Services;
using MortysDLP.Helpers;
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
            /* Sprachanpassung bei Window-Start */
            LanguageHelper.ApplyLanguage(LanguageHelper.ForceEnglish);

            InitializeComponent();
            SetUITexte();
            Loaded += async (_, __) => await LoadHistory();
        }

        private void SetUITexte()
        {
            this.Title = UITexte.UITexte.DownloadHistory_Title;
            this.ReuseButton.Content = UITexte.UITexte.DownloadHistory_Button_ReUse;
            this.ClearButton.Content = UITexte.UITexte.DownloadHistory_Button_Clear;
            this.EmptyText.Text = UITexte.UITexte.DownloadHistory_Label_EmptyHistory;
            this.InfoText.Text = UITexte.UITexte.DownloadHistory_Label_EmptyHistory_Info;
        }
        private async Task LoadHistory()
        {
            HistoryList.ItemsSource = await DownloadHistoryService.LoadAsync();
            UpdateButtonStates();
        }

        private async void Clear_Click(object sender, RoutedEventArgs e)
        {
            await DownloadHistoryService.ClearAsync();
            await LoadHistory();
        }

        private void Reuse_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryList.SelectedItem is DownloadHistoryEntry entry)
            {
                // MainWindow suchen und URL setzen
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow main)
                    {
                        main.tbURL.Text = entry.Url;
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
