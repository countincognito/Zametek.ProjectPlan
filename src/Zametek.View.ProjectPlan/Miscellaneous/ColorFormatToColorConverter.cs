using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Zametek.Common.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class ColorFormatToColorConverter
        : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var input = value as ColorFormatModel;
            if (input == null)
            {
                return DependencyProperty.UnsetValue;
            }
            return Color.FromArgb(input.A, input.R, input.G, input.B);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is Color))
            {
                return DependencyProperty.UnsetValue;
            }
            var input = (Color)value;
            return new ColorFormatModel
            {
                A = input.A,
                R = input.R,
                G = input.G,
                B = input.B
            };
        }

        #endregion
    }
}
