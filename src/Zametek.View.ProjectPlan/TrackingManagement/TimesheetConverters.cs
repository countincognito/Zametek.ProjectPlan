using Avalonia.Data.Converters;
using Avalonia.Media;
using Zametek.Common.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public static class TimesheetConverters
    {
        // Alpha overlays rather than solid fills so the chips read correctly
        // against both light and dark themes.
        private static readonly SolidColorBrush s_UnderBrush = new(Color.Parse(@"#40EF9F27"));
        private static readonly SolidColorBrush s_OverBrush = new(Color.Parse(@"#40E24B4A"));
        private static readonly SolidColorBrush s_FullBrush = new(Color.Parse(@"#4060EF40"));

        /// <summary>
        /// Background for a day-total chip: amber when the day is under-booked,
        /// red when over-booked, transparent otherwise.
        /// </summary>
        public static readonly IValueConverter DayLoadToBackground =
            new FuncValueConverter<TimesheetDayLoad, IBrush>(load => load switch
            {
                TimesheetDayLoad.Under => s_UnderBrush,
                TimesheetDayLoad.Over => s_OverBrush,
                TimesheetDayLoad.Full => s_FullBrush,
                _ => Brushes.Transparent,
            });
    }
}
