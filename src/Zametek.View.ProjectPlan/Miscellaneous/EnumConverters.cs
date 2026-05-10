using Avalonia.Data.Converters;
using Zametek.Common.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public static class EnumConverters
    {
        public static readonly IValueConverter IsNonWorkingDayModeMatch =
            new FuncValueConverter<NonWorkingDayMode, NonWorkingDayMode, bool>((x, y) => x == y);

        public static readonly IValueConverter IsRecurrenceFrequencyMatch =
            new FuncValueConverter<RecurrenceFrequency, RecurrenceFrequency, bool>((x, y) => x == y);

        public static readonly IValueConverter IsNotRecurrenceFrequencyMatch =
            new FuncValueConverter<RecurrenceFrequency, RecurrenceFrequency, bool>((x, y) => x != y);

        public static readonly IValueConverter IsShellViewMatch =
            new FuncValueConverter<ShellView, ShellView, bool>((x, y) => x == y);
    }
}
