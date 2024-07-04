namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectImportModel
    {
        public DateTimeOffset ProjectStart { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public List<ResourceModel> Resources { get; init; } = [];

        public double DefaultUnitCost { get; init; }

        public List<ActivitySeverityModel> ActivitySeverities { get; init; } = [];

        public List<WorkStreamModel> WorkStreams { get; init; } = [];
    }
}
