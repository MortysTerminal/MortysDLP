using System.Globalization;
using System.Windows.Data;

namespace MortysDLP
{
    public class BoolToAudioVideoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAudioOnly)
            {
                return isAudioOnly 
                    ? UITexte.UITextDictionary.Get("DownloadHistory.Type.Audio")
                    : UITexte.UITextDictionary.Get("DownloadHistory.Type.Video");
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
