using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The dates that the calendar can count to. The calendar counts each date of a plan from the project start, day by day,
    /// up to the limit of a time. zpp checks the dates of a project file before the load (PlanCheck), and the import of a
    /// workbook checks its dates at their cells. A workbook with the project start 9999-12-31 ended zpp with the text of a .NET
    /// exception, and the server answered 500 (from the review of 0.11.0-beta.2).
    /// </summary>
    public static class PlanDateCheck
    {
        /// <summary>
        /// The first day on which a project can start. With <see cref="LastProjectStart"/>, it leaves room for the days of a
        /// schedule inside the dates of .NET.
        /// </summary>
        public static readonly DateTime FirstProjectStart = new(1900, 1, 1);

        /// <summary>
        /// The last day on which a project can start.
        /// </summary>
        public static readonly DateTime LastProjectStart = new(2999, 12, 31);

        /// <summary>
        /// True when the day of the start is from <see cref="FirstProjectStart"/> to <see cref="LastProjectStart"/>.
        /// </summary>
        public static bool IsProjectStartInRange(DateTimeOffset start) =>
            start.Date >= FirstProjectStart
            && start.Date <= LastProjectStart;

        /// <summary>
        /// True when the day of the date is further from the day of the start than the limit of a time. Such a date cannot be
        /// a time of the plan, and the calendar would count to it day by day, or past the end of the dates of .NET.
        /// </summary>
        public static bool IsFarFromStart(DateTimeOffset start, DateTimeOffset date) =>
            Math.Abs((date.Date - start.Date).TotalDays) > GraphLimits.MaximumTimeValue;
    }
}
