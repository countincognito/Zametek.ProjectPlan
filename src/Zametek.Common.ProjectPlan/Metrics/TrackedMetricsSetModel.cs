namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record TrackedMetricsSetModel
    {
        public List<TrackedMetricsModel> TrackedMetrics { get; init; } = [];
    }
}
