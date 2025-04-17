using System;
using System.Globalization;
using System.Windows.Data;

namespace EliteInfoPanel.Converters
{
    public class TagFilterConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is ViewModels.SummaryItemViewModel item && parameter is string filter)
            {
                bool negate = filter.StartsWith("!");
                string tagToMatch = negate ? filter[1..] : filter;
                return negate ? item.Tag != tagToMatch : item.Tag == tagToMatch;
            }

            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
