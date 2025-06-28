namespace Zametek.Data.ProjectPlan.v0_4_2
{
    [Serializable]
    public record ProjectPlanModel
    {
        public string Version { get; } = Versions.v0_4_2;

        public DateTimeOffset ProjectStart { get; init; }

        public DateTimeOffset Today { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public v0_1_0.ArrowGraphSettingsModel ArrowGraphSettings { get; init; } = new();

        public v0_4_0.ResourceSettingsModel ResourceSettings { get; init; } = new();

        public v0_3_2.WorkStreamSettingsModel WorkStreamSettings { get; init; } = new();

        public v0_4_1.DisplaySettingsModel DisplaySettings { get; init; } = new();

        public GraphCompilationModel GraphCompilation { get; init; } = new();

        public v0_4_0.ArrowGraphModel ArrowGraph { get; init; } = new();

        public bool HasStaleOutputs { get; init; }
    }
}
