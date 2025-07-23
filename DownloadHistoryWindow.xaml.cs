using MortysDLP.Models;
using MortysDLP.Services;
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
            InitializeComponent();
            LoadHistory();
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
