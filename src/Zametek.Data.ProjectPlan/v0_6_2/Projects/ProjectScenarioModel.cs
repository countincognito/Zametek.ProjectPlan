namespace Zametek.Data.ProjectPlan.v0_6_2
{
    // v0.6.2 changes only the activities. Everything else in a scenario is the model of the
    // version that last changed it.
    [Serializable]
    public record ProjectScenarioModel
    {
        public DateTimeOffset ProjectStart { get; init; }

        public DateTimeOffset Today { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public v0_5_0.GraphSettingsModel GraphSettings { get; init; } = new();

        public v0_6_1.ResourceSettingsModel ResourceSettings { get; init; } = new();

        public v0_3_2.WorkStreamSettingsModel WorkStreamSettings { get; init; } = new();

        public v0_6_0.HolidaySettingsModel HolidaySettings { get; init; } = new();

        public v0_5_0.MetricsModel Metrics { get; init; } = new();

        public List<v0_6_1.ResourceMetricsModel> ResourceMetrics { get; init; } = [];

        public v0_6_1.ProjectScenarioDisplaySettingsModel DisplaySettings { get; init; } = new();

        public v0_6_1.GraphLayoutModel ArrowGraphLayout { get; init; } = new();

        public v0_6_1.GraphLayoutModel VertexGraphLayout { get; init; } = new();
    }
}
