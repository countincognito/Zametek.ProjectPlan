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
    }
}
