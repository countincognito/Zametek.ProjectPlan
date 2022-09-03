using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Zametek.View.ProjectPlan
{
    public class NullableIntConverter
        : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var stringValue = value as string;

            if (string.IsNullOrEmpty(stringValue)
                || !int.TryParse(stringValue, out int output))
            {
                return null;
            }

            return output;
        }
    }
}
