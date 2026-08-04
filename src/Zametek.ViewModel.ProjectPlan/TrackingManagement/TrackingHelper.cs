using System.Globalization;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Pure helper logic for the tracking panels: the shared day count, plus
    /// the effort timesheet's day-load classification and row labels.
    /// </summary>
    public static class TrackingHelper
    {
        /// <summary>
        /// The number of consecutive tracker days shown by the tracking
        /// panels (both the effort timesheet and the progress grid). The day
        /// columns deliberately extend past the visible edge, spreadsheet
        /// style, and the window is stepped one day at a time via pagination.
        /// </summary>
        public const int DayCount = 15;

        /// <summary>
        /// The booked percentage that counts as exactly one full day. The
        /// classification thresholds hang off this single value so they are
        /// easy to adjust later.
        /// </summary>
        public const int c_FullDayPercentage = 100;

        /// <summary>
        /// Classifies a resource's total booked percentage on a single day.
        /// Null means no bookings at all (distinct from an explicit zero,
        /// which counts as under-booked).
        /// </summary>
        public static TimesheetDayLoad Classify(int? totalPercentage)
        {
            if (totalPercentage is null)
            {
                return TimesheetDayLoad.None;
            }

            int total = totalPercentage.GetValueOrDefault();

            if (total < c_FullDayPercentage)
            {
                return TimesheetDayLoad.Under;
            }
            if (total > c_FullDayPercentage)
            {
                return TimesheetDayLoad.Over;
            }
            return TimesheetDayLoad.Full;
        }

        /// <summary>
        /// Builds the display label for a timesheet row, e.g. "12 - Backend".
        /// Falls back to just the ID when the activity has no name.
        /// </summary>
        public static string BuildActivityLabel(
            int activityId,
            string? activityName)
        {
            string id = activityId.ToString(CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(activityName) ? id : $@"{id} - {activityName}";
        }
    }
}
