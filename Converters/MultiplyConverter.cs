using System;
using System.Globalization;
using System.Windows.Data;

namespace EliteInfoPanel.Converters
{
    public class MultiplyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue && parameter != null)
            {
                if (double.TryParse(parameter.ToString(), out double multiplier))
                {
                    return doubleValue * multiplier;
                }
            }
            else if (value is int intValue && parameter != null)
            {
                if (double.TryParse(parameter.ToString(), out double multiplier))
                {
                    return intValue * multiplier;
                }
            }

            return value; // Return original value if conversion fails
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue && parameter != null)
            {
                if (double.TryParse(parameter.ToString(), out double multiplier) && multiplier != 0)
                {
                    return doubleValue / multiplier;
                }
            }

            return value; // Return original value if conversion fails
        }
    }
}
