using Avalonia.Data.Converters;
using System;

namespace Zametek.View.ProjectPlan
{
    public static class StringConverters
    {
        public static readonly IValueConverter IsMatch =
            new FuncValueConverter<string?, string?, bool>(
                (x, y) => string.Equals(x, y, StringComparison.InvariantCulture));
    }
}
