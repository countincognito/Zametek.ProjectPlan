namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record TrackingSeriesSetModel
    {
        public List<TrackingPointModel> Plan { get; init; } = new List<TrackingPointModel>();
        public List<TrackingPointModel> PlanProjection { get; init; } = new List<TrackingPointModel>();

        public List<TrackingPointModel> Progress { get; init; } = new List<TrackingPointModel>();
        public List<TrackingPointModel> ProgressProjection { get; init; } = new List<TrackingPointModel>();

        public List<TrackingPointModel> Effort { get; init; } = new List<TrackingPointModel>();
        public List<TrackingPointModel> EffortProjection { get; init; } = new List<TrackingPointModel>();
    }
}
