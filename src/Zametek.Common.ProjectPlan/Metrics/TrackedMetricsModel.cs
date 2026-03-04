namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record TrackedMetricsModel
    {
        public Guid NodeId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;

        public MetricsModel Metrics { get; init; } = new();
    }
}
