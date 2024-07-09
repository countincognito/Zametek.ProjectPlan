using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using Zametek.Common.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class ColorFormatToColorConverter
        : IValueConverter
    {
        #region IValueConverter Members

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not ColorFormatModel input)
            {
                return AvaloniaProperty.UnsetValue;
            }
            return Color.FromArgb(input.A, input.R, input.G, input.B);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Color)
            {
                return AvaloniaProperty.UnsetValue;
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
