using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;
using Zametek.Common.ProjectPlan;

namespace Zametek.Graphs.ProjectPlan
{
    public class ThemeToBackgroundConverter
        : IValueConverter
    {
        private static readonly IBrush s_LightThemeBackground = new SolidColorBrush(ColorHelper.LightThemeBackground);
        private static readonly IBrush s_DarkThemeBackground = new SolidColorBrush(ColorHelper.DarkThemeBackground);

        #region IValueConverter Members

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is BaseTheme baseTheme)
            {
                if (baseTheme == BaseTheme.Light)
                {
                    return s_LightThemeBackground;
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
