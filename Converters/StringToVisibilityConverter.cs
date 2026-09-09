using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MortysDLP
{
    /// <summary>
    /// <see cref="Visibility.Visible"/>, wenn der gebundene Text nicht leer ist, sonst
    /// <see cref="Visibility.Collapsed"/>. Für Verlaufseinträge aus der Zeit vor dem
    /// mitgespeicherten Zielordner (dort ist <c>DownloadDirectory</c> leer/null).
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
