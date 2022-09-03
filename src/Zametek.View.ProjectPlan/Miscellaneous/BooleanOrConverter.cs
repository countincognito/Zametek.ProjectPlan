using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zametek.View.ProjectPlan
{
    internal class BooleanOrConverter
        : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            return values.OfType<IConvertible>().Any(System.Convert.ToBoolean);
        }
    }
}
