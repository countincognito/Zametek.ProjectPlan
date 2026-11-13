namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectScenarioModel
    {
        public DateTimeOffset ProjectStart { get; init; }

        // Calculated: the first working day on ProjectStart or after it, which is day 0 of the schedule. A load calculates
        // it again, so a value that a file gives changes nothing.
        public DateTimeOffset? EffectiveProjectStart { get; init; }

        public DateTimeOffset Today { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public GraphSettingsModel GraphSettings { get; init; } = new();

        public ResourceSettingsModel ResourceSettings { get; init; } = new();

        public WorkStreamSettingsModel WorkStreamSettings { get; init; } = new();

        public HolidaySettingsModel HolidaySettings { get; init; } = new();

        public MetricsModel Metrics { get; init; } = new();

        public List<ResourceMetricsModel> ResourceMetrics { get; init; } = [];

        public SimulationSettingsModel SimulationSettings { get; init; } = new();

        // What the last run of the simulation found, or null when the scenario has none.
        public SimulationSummaryModel? SimulationSummary { get; init; }

        public ProjectScenarioDisplaySettingsModel DisplaySettings { get; init; } = new();

        public GraphLayoutModel ArrowGraphLayout { get; init; } = new();

        public GraphLayoutModel VertexGraphLayout { get; init; } = new();
    }
}
