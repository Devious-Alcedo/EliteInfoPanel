using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EliteInfoPanel.Converters
{
    public class RectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4 ||
                values[0] is not double completionPercentage ||
                values[1] is not double totalPercentage ||
                values[2] is not double totalWidth ||
                values[3] is not double height)
                return new Rect(0, 0, 0, 0);

            // Calculate the starting X position based on completion
            double startX = (completionPercentage / 100.0) * totalWidth;

            // Calculate the width of the available portion
            double availableWidth = ((totalPercentage - completionPercentage) / 100.0) * totalWidth;

            // Ensure values are valid
            startX = Math.Max(0, startX);
            availableWidth = Math.Max(0, availableWidth);

            return new Rect(startX, 0, availableWidth, height);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}