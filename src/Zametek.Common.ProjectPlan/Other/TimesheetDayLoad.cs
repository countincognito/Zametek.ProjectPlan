namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// Classifies a resource's total booked percentage on a single day of the
    /// effort timesheet.
    /// </summary>
    [Serializable]
    public enum TimesheetDayLoad
    {
        /// <summary>
        /// No bookings at all (distinct from an explicit zero).
        /// </summary>
        None,
        /// <summary>
        /// Booked below a full day (including an explicit zero).
        /// </summary>
        Under,
        /// <summary>
        /// Booked at exactly a full day.
        /// </summary>
        Full,
        /// <summary>
        /// Booked beyond a full day.
        /// </summary>
        Over,
    }
}
