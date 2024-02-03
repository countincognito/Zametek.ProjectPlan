using Avalonia.Data.Converters;
using Avalonia.Input;
using System;
using System.Globalization;

namespace Zametek.View.ProjectPlan
{
    public class IsBusyCursorConverter
        : IValueConverter
    {
        #region IValueConverter Members

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not IConvertible)
            {
                return StandardCursorType.None;
            }

            return System.Convert.ToBoolean(value) ? StandardCursorType.Wait : StandardCursorType.Arrow;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
