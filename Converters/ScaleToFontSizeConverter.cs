using System;
using System.Globalization;
using System.Windows.Data;

namespace EliteInfoPanel.Converters
{
    public class ScaleToFontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double scale && parameter != null)
            {
                double baseFontSize;
                if (parameter is double dParam)
                {
                    baseFontSize = dParam;
                }
                else if (double.TryParse(parameter.ToString(), out double parsedValue))
                {
                    baseFontSize = parsedValue;
                }
                else
                {
                    baseFontSize = 14.0; // Default if parsing fails
                }

                return baseFontSize * scale;
            }

            return 14.0; // Default font size
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double fontSize && parameter != null)
            {
                double baseFontSize;
                if (parameter is double dParam)
                {
                    baseFontSize = dParam;
                }
                else if (double.TryParse(parameter.ToString(), out double parsedValue))
                {
                    baseFontSize = parsedValue;
                }
                else
                {
                    baseFontSize = 14.0; // Default if parsing fails
                }

                return fontSize / baseFontSize;
            }

            return 1.0; // Default scale
        }
    }
}