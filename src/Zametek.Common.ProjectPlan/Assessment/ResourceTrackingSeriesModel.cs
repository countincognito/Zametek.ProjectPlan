namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// The tracking series (Plan, Progress and Effort, plus their projections)
    /// attributable to a single resource. The point values are raw working-time
    /// numerators; the recorded percentages are shares of the whole project
    /// (i.e. Value / TrackingSeriesSetModel.TotalWorkingTime), so the series
    /// across all resources sum back to the aggregate series. To normalise a
    /// resource against its own plan instead, recompute percentages using
    /// TotalWorkingTime on this model as the denominator.
    /// </summary>
    [Serializable]
    public record ResourceTrackingSeriesModel
    {
        public int ResourceId { get; init; }

        public string ResourceName { get; init; } = string.Empty;

        public ColorFormatModel ColorFormat { get; init; } = new ColorFormatModel();

        public int DisplayOrder { get; init; }

        /// <summary>
        /// The total working time planned for this resource alone
        /// (i.e. the sum of the durations of the activities it is allocated to).
        /// </summary>
        public double TotalWorkingTime { get; init; }

        public List<TrackingPointModel> Plan { get; init; } = [];
        public List<TrackingPointModel> PlanProjection { get; init; } = [];

        public List<TrackingPointModel> Progress { get; init; } = [];
        public List<TrackingPointModel> ProgressProjection { get; init; } = [];

        public List<TrackingPointModel> Effort { get; init; } = [];
        public List<TrackingPointModel> EffortProjection { get; init; } = [];
    }
}
