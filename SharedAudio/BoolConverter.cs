using System.Globalization;
using System.Windows.Data;

namespace SharedAudio
{
    public class BoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolvalue)
            {
                if ((string)parameter == "invert")
                {
                    return !boolvalue;
                }
                return boolvalue;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolvalue)
            {
                if ((string)parameter == "invert")
                {
                    return !boolvalue;
                }
                return boolvalue;
            }

            return false;
        }
    }
}
