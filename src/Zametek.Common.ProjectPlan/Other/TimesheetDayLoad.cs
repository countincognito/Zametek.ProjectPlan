namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// Classifies a tracked percentage against a full 100%: a resource's
    /// total booked percentage on a single day of the effort timesheet, or
    /// an activity's completion.
    /// </summary>
    [Serializable]
    public enum TimesheetDayLoad
    {
        /// <summary>
        /// No value at all (distinct from an explicit zero).
        /// </summary>
        None,
        /// <summary>
        /// Below the full 100% (including an explicit zero).
        /// </summary>
        Under,
        /// <summary>
        /// At exactly the full 100%.
        /// </summary>
        Full,
        /// <summary>
        /// Beyond the full 100%.
        /// </summary>
        Over,
    }
}
