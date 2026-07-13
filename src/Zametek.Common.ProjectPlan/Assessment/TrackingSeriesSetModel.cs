namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record TrackingSeriesSetModel
    {
        public List<TrackingPointModel> Plan { get; init; } = [];
        public List<TrackingPointModel> PlanProjection { get; init; } = [];

        public List<TrackingPointModel> Progress { get; init; } = [];
        public List<TrackingPointModel> ProgressProjection { get; init; } = [];

        public List<TrackingPointModel> Effort { get; init; } = [];
        public List<TrackingPointModel> EffortProjection { get; init; } = [];

        /// <summary>
        /// The total working time planned across the whole project
        /// (i.e. the sum over all activities of allocated-resource count x duration).
        /// This is the denominator behind the percentages in the series above.
        /// </summary>
        public double TotalWorkingTime { get; init; }

        /// <summary>
        /// The same series broken down by resource, in display order.
        /// The per-resource values sum back to the aggregate series above.
        /// </summary>
        public List<ResourceTrackingSeriesModel> ByResource { get; init; } = [];
    }
}
