using Avalonia;
using Avalonia.Controls.Primitives;
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
            return ViewModel.ProjectPlan.ColorHelper.ColorFormatToAvaloniaColor(input);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Color)
            {
                return AvaloniaProperty.UnsetValue;
            }
            var input = (Color)value;
            return ViewModel.ProjectPlan.ColorHelper.AvaloniaColorToColorFormatModel(input);
        }

        #endregion
    }
}
