namespace Zametek.Data.ProjectPlan.v0_5_0
{
    [Serializable]
    public record ProjectPlanModel
    {
        public string Version { get; } = Versions.v0_5_0;

        public DateTimeOffset ProjectStart { get; init; }

        public DateTimeOffset Today { get; init; }

        public List<v0_4_4.DependentActivityModel> DependentActivities { get; init; } = [];

        public v0_1_0.ArrowGraphSettingsModel ArrowGraphSettings { get; init; } = new();

        public v0_4_4.ResourceSettingsModel ResourceSettings { get; init; } = new();

        public v0_3_2.WorkStreamSettingsModel WorkStreamSettings { get; init; } = new();

        public DisplaySettingsModel DisplaySettings { get; init; } = new();
    }
}
