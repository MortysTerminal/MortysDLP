using MortysDLP.UITexte;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MortysDLP.Views
{
    public partial class TimelineWindow : Window
    {
        private readonly TimeSpan _totalDuration;
        private bool _suppressSync = true;
        private bool _initialized;
        private Slider? _activeSlider;

        public TimeSpan SelectedStart { get; private set; }
        public TimeSpan SelectedEnd { get; private set; }
        public bool Confirmed { get; private set; }

        public TimelineWindow(TimeSpan totalDuration, TimeSpan? initialStart = null, TimeSpan? initialEnd = null)
        {
            _totalDuration = totalDuration > TimeSpan.Zero ? totalDuration : TimeSpan.FromHours(1);

            InitializeComponent();

            sliderStart.Maximum = _totalDuration.TotalSeconds;
            sliderEnd.Maximum = _totalDuration.TotalSeconds;
            sliderStart.Value = initialStart?.TotalSeconds ?? 0;
            sliderEnd.Value = initialEnd?.TotalSeconds ?? _totalDuration.TotalSeconds;

            SetUITexts();

            _initialized = true;
            _suppressSync = false;

            UpdateLabels();
            SyncManualFields();
        }

        private void SetUITexts()
        {
            var T = UITextDictionary.Get;
            txtHeaderTitle.Text = T("Timeline.Title");
            txtVideoDuration.Text = string.Format(T("Timeline.TotalDuration"), FormatTime(_totalDuration));
            txtManualFrom.Text = T("Timeline.From");
            txtManualTo.Text = T("Timeline.To");
            btnApply.Content = T("Timeline.Apply");
            btnCancel.Content = T("Timeline.Cancel");
        }

        private void SliderStart_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSync) return;

            if (sliderStart.Value > sliderEnd.Value - 1)
                sliderStart.Value = sliderEnd.Value - 1;

            UpdateLabels();
            SyncManualFields();
        }

        private void SliderEnd_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSync) return;

            if (sliderEnd.Value < sliderStart.Value + 1)
                sliderEnd.Value = sliderStart.Value + 1;

            UpdateLabels();
            SyncManualFields();
        }

        private void ManualTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressSync) return;

            if (TryParseTime(tbManualStart.Text, out var start) &&
                TryParseTime(tbManualEnd.Text, out var end) &&
                start < end && end <= _totalDuration)
            {
                _suppressSync = true;
                sliderStart.Value = start.TotalSeconds;
                sliderEnd.Value = end.TotalSeconds;
                _suppressSync = false;
                UpdateLabels();
            }
        }

        private void UpdateLabels()
        {
            if (!_initialized || sliderStart == null || sliderEnd == null ||
                txtStartLabel == null || txtEndLabel == null || txtSelectionDuration == null)
                return;

            var start = TimeSpan.FromSeconds(sliderStart.Value);
            var end = TimeSpan.FromSeconds(sliderEnd.Value);
            var duration = end - start;

            txtStartLabel.Text = $"▶ {FormatTime(start)}";
            txtEndLabel.Text = $"{FormatTime(end)} ◀";
            txtSelectionDuration.Text = string.Format(UITextDictionary.Get("Timeline.Selection"), FormatTime(duration));

            UpdateSelectedRangeVisual();
        }

        private void UpdateSelectedRangeVisual()
        {
            if (canvasTrack == null || selectedRange == null || _totalDuration.TotalSeconds <= 0) return;

            double trackWidth = canvasTrack.ActualWidth;
            if (trackWidth <= 0) return;

            double startFrac = sliderStart.Value / _totalDuration.TotalSeconds;
            double endFrac = sliderEnd.Value / _totalDuration.TotalSeconds;

            Canvas.SetLeft(selectedRange, startFrac * trackWidth);
            selectedRange.Width = Math.Max(0, (endFrac - startFrac) * trackWidth);
        }

        private void canvasTrack_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateSelectedRangeVisual();

        private void TrackGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            double trackWidth = gridTrack.ActualWidth;
            if (trackWidth <= 0) return;

            double clickValue = (e.GetPosition(gridTrack).X / trackWidth) * _totalDuration.TotalSeconds;
            clickValue = Math.Clamp(clickValue, 0, _totalDuration.TotalSeconds);

            double distToStart = Math.Abs(clickValue - sliderStart.Value);
            double distToEnd = Math.Abs(clickValue - sliderEnd.Value);

            _activeSlider = distToStart <= distToEnd ? sliderStart : sliderEnd;
            _activeSlider.Value = clickValue;

            gridTrack.CaptureMouse();
            e.Handled = true;
        }

        private void TrackGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_activeSlider == null || e.LeftButton != MouseButtonState.Pressed) return;

            double trackWidth = gridTrack.ActualWidth;
            if (trackWidth <= 0) return;

            double value = (e.GetPosition(gridTrack).X / trackWidth) * _totalDuration.TotalSeconds;
            _activeSlider.Value = Math.Clamp(value, 0, _totalDuration.TotalSeconds);
        }

        private void TrackGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _activeSlider = null;
            gridTrack.ReleaseMouseCapture();
        }

        private void canvasTicks_SizeChanged(object sender, SizeChangedEventArgs e) => DrawTicks();

        private void DrawTicks()
        {
            canvasTicks.Children.Clear();
            double width = canvasTicks.ActualWidth;
            if (width <= 0 || _totalDuration.TotalSeconds <= 0) return;

            double totalSec = _totalDuration.TotalSeconds;
            double interval = GetTickInterval(totalSec);

            for (double sec = 0; sec <= totalSec; sec += interval)
            {
                double x = (sec / totalSec) * width;

                var line = new System.Windows.Shapes.Line
                {
                    X1 = x, X2 = x,
                    Y1 = 0, Y2 = 6,
                    Stroke = new SolidColorBrush(Colors.Gray),
                    StrokeThickness = 1,
                    SnapsToDevicePixels = true
                };
                canvasTicks.Children.Add(line);

                var label = new TextBlock
                {
                    Text = FormatTime(TimeSpan.FromSeconds(sec)),
                    FontSize = 9,
                    Opacity = 0.6,
                    FontFamily = new FontFamily("Consolas")
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
                Canvas.SetTop(label, 7);
                canvasTicks.Children.Add(label);
            }
        }

        private static double GetTickInterval(double totalSeconds)
        {
            return totalSeconds switch
            {
                <= 60 => 10,
                <= 300 => 30,
                <= 600 => 60,
                <= 1800 => 300,
                <= 3600 => 600,
                <= 7200 => 900,
                _ => 1800
            };
        }

        private void SyncManualFields()
        {
            if (tbManualStart == null || tbManualEnd == null || sliderStart == null || sliderEnd == null)
                return;

            _suppressSync = true;
            tbManualStart.Text = FormatTime(TimeSpan.FromSeconds(sliderStart.Value));
            tbManualEnd.Text = FormatTime(TimeSpan.FromSeconds(sliderEnd.Value));
            _suppressSync = false;
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            SelectedStart = TimeSpan.FromSeconds(sliderStart.Value);
            SelectedEnd = TimeSpan.FromSeconds(sliderEnd.Value);
            Confirmed = true;
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }

        private static string FormatTime(TimeSpan ts)
            => ts.TotalHours >= 1
                ? ts.ToString(@"hh\:mm\:ss")
                : ts.ToString(@"mm\:ss");

        private static bool TryParseTime(string input, out TimeSpan result)
        {
            string[] formats = [@"hh\:mm\:ss\.ff", @"hh\:mm\:ss", @"mm\:ss\.ff", @"mm\:ss"];
            foreach (var fmt in formats)
            {
                if (TimeSpan.TryParseExact(input, fmt, CultureInfo.InvariantCulture, out result))
                    return true;
            }
            return TimeSpan.TryParse(input, out result);
        }
    }
}
