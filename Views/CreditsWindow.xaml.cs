using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MortysDLP.Views
{
    public partial class CreditsWindow : Window
    {
        private record ToolInfo(
            string Name,
            string Description,
            string License,
            string GitHubUrl,
            string? HomepageUrl = null);

        private static readonly ToolInfo[] Tools =
        [
            new(
                "yt-dlp",
                "Kernmodul für alle Video- und Audio-Downloads. yt-dlp ist ein Feature-reiches Fork von youtube-dl mit aktivem Entwicklungsteam. Es unterstützt hunderte von Plattformen (YouTube, Twitch, Vimeo u.v.m.) und bildet das Herzstück von MortysDLP.",
                "Unlicense",
                "https://github.com/yt-dlp/yt-dlp"),

            new(
                "ffmpeg",
                "Industriestandard für Multimedia-Verarbeitung. Wird für das Zusammenführen von Video-/Audiospuren, Konvertierungen, Codec-Prüfungen und die Erstellung von GIFs verwendet.",
                "LGPL 2.1 / GPL 2+",
                "https://github.com/FFmpeg/FFmpeg",
                "https://ffmpeg.org"),

            new(
                "ffprobe",
                "Teil des ffmpeg-Projekts. Analysiert Metadaten von Mediendateien (Codec, Samplerate, Kanäle) und wird von MortysDLP für die intelligente Audio-Verarbeitungsentscheidung genutzt.",
                "LGPL 2.1 / GPL 2+",
                "https://github.com/FFmpeg/FFmpeg",
                "https://ffmpeg.org"),

            new(
                "TwitchDownloaderCLI",
                "Spezialisiertes CLI-Tool für Twitch-Chats. Wird ausschließlich für den Chat-Download (JSON) und das Chat-Rendering (MP4-Overlay-Video) verwendet. Das Video-Download läuft über yt-dlp.",
                "MIT",
                "https://github.com/lay295/TwitchDownloader"),

            new(
                "Whisper.net / whisper.cpp",
                "KI-basierte Sprach-zu-Text-Transkription (OpenAI Whisper). Wird auf der Transkribieren-Seite verwendet, um Audio- und Videodateien lokal in Text umzuwandeln – ohne Cloud, ohne Datenweitergabe.",
                "MIT",
                "https://github.com/sandrohanea/whisper.net",
                "https://github.com/ggml-org/whisper.cpp"),

            new(
                "Wpf.Ui",
                "WPF-Bibliothek mit modernem Fluent-Design (Windows 11-Stil). Stellt Farbpaletten, Schriften und UI-Designressourcen bereit, die das Erscheinungsbild von MortysDLP prägen.",
                "MIT",
                "https://github.com/lepoco/wpfui"),
        ];

        public CreditsWindow()
        {
            InitializeComponent();
            BuildToolCards();
        }

        private void BuildToolCards()
        {
            foreach (var tool in Tools)
            {
                var card = new Border
                {
                    Background     = (Brush)FindResource("LayerFillColorDefaultBrush"),
                    BorderBrush    = (Brush)FindResource("CardStrokeColorDefaultBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius   = new CornerRadius(8),
                    Padding        = new Thickness(14, 10, 14, 10),
                    Margin         = new Thickness(0, 0, 0, 8),
                };

                var inner = new StackPanel();

                // Tool-Name + Lizenz-Badge
                var headerRow = new Grid();
                headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameText = new TextBlock
                {
                    Text       = tool.Name,
                    FontSize   = 14,
                    FontWeight = FontWeights.SemiBold,
                };
                Grid.SetColumn(nameText, 0);

                var licenseBadge = new Border
                {
                    Background    = new SolidColorBrush(Color.FromRgb(40, 50, 70)),
                    CornerRadius  = new CornerRadius(4),
                    Padding       = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text     = tool.License,
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(130, 175, 230)),
                    }
                };
                Grid.SetColumn(licenseBadge, 1);

                headerRow.Children.Add(nameText);
                headerRow.Children.Add(licenseBadge);
                inner.Children.Add(headerRow);

                // Beschreibung
                inner.Children.Add(new TextBlock
                {
                    Text           = tool.Description,
                    FontSize       = 12,
                    TextWrapping   = TextWrapping.Wrap,
                    Margin         = new Thickness(0, 5, 0, 6),
                    Opacity        = 0.75,
                });

                // Links
                var linkRow = new StackPanel { Orientation = Orientation.Horizontal };

                AddLinkButton(linkRow, "GitHub", tool.GitHubUrl);

                if (tool.HomepageUrl != null)
                {
                    linkRow.Children.Add(new TextBlock
                    {
                        Text       = " · ",
                        FontSize   = 11,
                        Opacity    = 0.4,
                        VerticalAlignment = VerticalAlignment.Center,
                    });
                    AddLinkButton(linkRow, "Website", tool.HomepageUrl);
                }

                inner.Children.Add(linkRow);
                card.Child = inner;
                spTools.Children.Add(card);
            }
        }

        private void AddLinkButton(StackPanel parent, string label, string url)
        {
            var btn = new Button
            {
                Content     = $"→ {label}: {url}",
                Tag         = url,
                Style       = (Style)FindResource("LinkButtonStyle"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            btn.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
                catch { }
            };
            parent.Children.Add(btn);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
