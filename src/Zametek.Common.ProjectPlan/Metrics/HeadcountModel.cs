namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// The headcount of the schedule: the number of resources that are allocated on each day. The model holds its peak, its
    /// average over the days of the schedule, and the ratio of the two. Righting Software uses the ratio to find a plan that
    /// needs a team that grows too fast.
    /// </summary>
    [Serializable]
    public record HeadcountModel
    {
        /// <summary>
        /// The most resources that are allocated on one day.
        /// </summary>
        public int? Peak { get; init; }

        /// <summary>
        /// The mean of the headcount of each day of the schedule.
        /// </summary>
        public double? Average { get; init; }

        /// <summary>
        /// The peak divided by the average, or null when the average is 0.
        /// </summary>
        public double? PeakToAverageRatio { get; init; }
    }
}
