namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectScenarioModel
    {
        public DateTimeOffset ProjectStart { get; init; }

        public DateTimeOffset Today { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public GraphSettingsModel GraphSettings { get; init; } = new GraphSettingsModel();

        public ResourceSettingsModel ResourceSettings { get; init; } = new ResourceSettingsModel();

        public WorkStreamSettingsModel WorkStreamSettings { get; init; } = new WorkStreamSettingsModel();

        public MetricsModel Metrics { get; init; } = new MetricsModel();

        public DisplaySettingsModel DisplaySettings { get; init; } = new DisplaySettingsModel();
    }
}
