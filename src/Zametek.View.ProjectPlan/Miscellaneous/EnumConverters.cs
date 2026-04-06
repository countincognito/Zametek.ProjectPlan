using Avalonia.Data.Converters;
using Zametek.Common.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public static class EnumConverters
    {
        public static readonly IValueConverter IsNonWorkingDayModeMatch =
            new FuncValueConverter<NonWorkingDayMode, NonWorkingDayMode, bool>((x, y) => x == y);

        public static readonly IValueConverter IsRecurrenceFrequencyTypeMatch =
            new FuncValueConverter<RecurrenceFrequencyType, RecurrenceFrequencyType, bool>((x, y) => x == y);

        public static readonly IValueConverter IsNotRecurrenceFrequencyTypeMatch =
            new FuncValueConverter<RecurrenceFrequencyType, RecurrenceFrequencyType, bool>((x, y) => x != y);
    }
}
