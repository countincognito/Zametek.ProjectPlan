using Avalonia.Data.Converters;
using Avalonia.Media;
using Zametek.Common.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    /// <summary>
    /// Shared value converters for the tracking surfaces (effort timesheet,
    /// progress tracker and activities grid): percentage-progress color coding.
    /// </summary>
    public static class TrackingConverters
    {
        // Alpha overlays rather than solid fills so the coloring reads
        // correctly against both light and dark themes (and composites over
        // row hover and selection backgrounds in the grids).
        private static readonly SolidColorBrush s_UnderBrush = new(Color.Parse(@"#40EF9F27"));
        private static readonly SolidColorBrush s_OverBrush = new(Color.Parse(@"#40E24B4A"));
        private static readonly SolidColorBrush s_FullBrush = new(Color.Parse(@"#4060EF40"));

        private static IBrush ToBrush(TimesheetDayLoad load) => load switch
        {
            TimesheetDayLoad.Under => s_UnderBrush,
            TimesheetDayLoad.Over => s_OverBrush,
            TimesheetDayLoad.Full => s_FullBrush,
            _ => Brushes.Transparent,
        };

        /// <summary>
        /// Background for a timesheet day-total chip: amber when the day is
        /// under-booked, red when over-booked, green when exactly full,
        /// transparent otherwise.
        /// </summary>
        public static readonly IValueConverter DayLoadToBackground =
            new FuncValueConverter<TimesheetDayLoad, IBrush>(ToBrush);

        /// <summary>
        /// Background for a completion percentage (e.g. an activity's last
        /// tracked value): amber below 100%, green at exactly 100%, red
        /// above, transparent when there is no value at all. Shares the
        /// timesheet's classification thresholds and brushes.
        /// </summary>
        public static readonly IValueConverter PercentageToBackground =
            new FuncValueConverter<int?, IBrush>(percentage => ToBrush(TrackingHelper.Classify(percentage)));
    }
}
