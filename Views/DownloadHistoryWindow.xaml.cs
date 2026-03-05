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
            /* Sprache wurde bereits in App.xaml.cs gesetzt */
            InitializeComponent();
            SetUITexte();
            Loaded += async (_, __) => await LoadHistory();
        }

        private void SetUITexte()
        {
            var T = UITexte.UITextDictionary.Get;
            
            // Window Title
            this.Title = T("DownloadHistory.Title");
            
            // Header
            txtHeaderTitle.Text = T("DownloadHistory.Header.Title");
            txtHeaderSubtitle.Text = T("DownloadHistory.Header.Subtitle");
            
            // Buttons
            this.ReuseButton.Content = T("DownloadHistory.Button.ReUse");
            this.ClearButton.Content = T("DownloadHistory.Button.Clear");
            
            // Empty State
            this.EmptyText.Text = T("DownloadHistory.Label.EmptyHistory");
            this.InfoText.Text = T("DownloadHistory.Label.EmptyHistory.Info");
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
                        main.DownloadPage.SetDownloadUrl(entry.Url);
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
            EmptyStatePanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
