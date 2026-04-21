namespace Zametek.Data.ProjectPlan.v0_6_0
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

        public HolidaySettingsModel HolidaySettings { get; init; } = new();

        public v0_5_0.MetricsModel Metrics { get; init; } = new();

        public DisplaySettingsModel DisplaySettings { get; init; } = new();
    }
}
