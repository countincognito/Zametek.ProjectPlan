namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectScenarioModel
    {
        public DateTimeOffset ProjectStart { get; init; }

        public DateTimeOffset Today { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public GraphSettingsModel GraphSettings { get; init; } = new();

        public ResourceSettingsModel ResourceSettings { get; init; } = new();

        public WorkStreamSettingsModel WorkStreamSettings { get; init; } = new();

        public HolidaySettingsModel HolidaySettings { get; init; } = new();

        public MetricsModel Metrics { get; init; } = new();

        public ProjectScenarioDisplaySettingsModel DisplaySettings { get; init; } = new();
    }
}
