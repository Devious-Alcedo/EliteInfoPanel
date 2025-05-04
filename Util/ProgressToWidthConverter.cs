using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace EliteInfoPanel.Util
{
    public class ProgressToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double progress = System.Convert.ToDouble(value);
            double totalWidth = parameter != null
                ? System.Convert.ToDouble(parameter)
                : 200; // Default width if no parameter

            return progress * totalWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // For multi-value conversion (when binding to actual width)
    public class ProgressToWidthMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] == null || values[1] == null)
                return 0.0;

            try
            {
                double ratio = System.Convert.ToDouble(values[0]);
                double totalWidth = System.Convert.ToDouble(values[1]);

                // If we have a ProgressBar with Maximum=100, normalize the value
                // to a ratio between 0 and 1
                if (values.Length >= 3 && values[2] != null)
                {
                    double maximum = System.Convert.ToDouble(values[2]);
                    if (maximum > 0)
                        ratio = ratio / maximum;
                }
                else if (ratio > 1.0)
                {
                    // If no Maximum is provided but ratio > 1, assume it's a percentage
                    ratio = ratio / 100.0;
                }

                // Ensure ratio is between 0 and 1
                ratio = Math.Max(0.0, Math.Min(1.0, ratio));

                return ratio * totalWidth;
            }
            catch
            {
                return 0.0;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
