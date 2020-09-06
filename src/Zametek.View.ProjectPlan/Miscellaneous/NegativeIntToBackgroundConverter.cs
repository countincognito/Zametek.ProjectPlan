using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Zametek.View.ProjectPlan
{
    public class NegativeIntToBackgroundConverter
        : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int? input = (int?)value;
            if (input == null)
            {
                return DependencyProperty.UnsetValue;
            }
            if (input < 0)
            {
                return Brushes.Red;
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
