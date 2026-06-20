namespace Zametek.Data.ProjectPlan.v0_6_1
{
    [Serializable]
    public record ProjectScenarioModel
    {
        public DateTimeOffset ProjectStart { get; init; }

        public DateTimeOffset Today { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public v0_5_0.GraphSettingsModel GraphSettings { get; init; } = new();

        public v0_4_4.ResourceSettingsModel ResourceSettings { get; init; } = new();

        public v0_3_2.WorkStreamSettingsModel WorkStreamSettings { get; init; } = new();

        public v0_6_0.HolidaySettingsModel HolidaySettings { get; init; } = new();

        public v0_5_0.MetricsModel Metrics { get; init; } = new();

        public ProjectScenarioDisplaySettingsModel DisplaySettings { get; init; } = new();

        public GraphLayoutModel ArrowGraphLayout { get; init; } = new();

        public GraphLayoutModel VertexGraphLayout { get; init; } = new();
    }
}
