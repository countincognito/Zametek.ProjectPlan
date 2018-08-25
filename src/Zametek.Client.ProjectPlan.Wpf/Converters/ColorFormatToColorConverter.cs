using System;
using System.Windows.Media;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Zametek.Common.Project;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ColorFormatToColorConverter
        : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var input = value as ColorFormatDto;
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
            return new ColorFormatDto
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
