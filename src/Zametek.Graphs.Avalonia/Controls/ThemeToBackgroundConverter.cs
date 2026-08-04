using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System.Globalization;

namespace Zametek.Graphs.Avalonia
{
    public class ThemeToBackgroundConverter
        : IValueConverter
    {
        // Immutable so the statics carry no thread ownership, whichever thread first touches this
        // type (see the THREADING note on GraphAppearance).
        private static readonly IBrush s_LightThemeBackground = new ImmutableSolidColorBrush(ColorHelper.LightThemeBackground);
        private static readonly IBrush s_DarkThemeBackground = new ImmutableSolidColorBrush(ColorHelper.DarkThemeBackground);

        #region IValueConverter Members

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is GraphTheme theme)
            {
                if (theme == GraphTheme.Light)
                {
                    return s_LightThemeBackground;
                }
                if (theme == GraphTheme.Dark)
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
