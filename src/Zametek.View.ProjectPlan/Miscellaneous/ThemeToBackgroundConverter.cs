using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using Zametek.Common.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class ThemeToBackgroundConverter
        : IValueConverter
    {
        // This matches s_SvgDarkThemeBackground in ArrowGraphSerializer
        private static readonly IBrush s_DarkThemeBackground = new SolidColorBrush(Color.FromArgb(255, 55, 55, 55));

        #region IValueConverter Members

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is BaseTheme baseTheme)
            {
                if (baseTheme == BaseTheme.Light)
                {
                    return Brushes.White;
                }
                if (baseTheme == BaseTheme.Dark)
                {
                    return s_DarkThemeBackground;
                }
            }

            return AvaloniaProperty.UnsetValue;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
