namespace Zametek.Data.ProjectPlan.v0_4_0
{
    [Serializable]
    public record ProjectPlanModel
    {
        public string Version { get; } = Versions.v0_4_0;

        public DateTimeOffset ProjectStart { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public v0_1_0.ArrowGraphSettingsModel ArrowGraphSettings { get; init; } = new v0_1_0.ArrowGraphSettingsModel();

        public ResourceSettingsModel ResourceSettings { get; init; } = new ResourceSettingsModel();

        public v0_3_2.WorkStreamSettingsModel WorkStreamSettings { get; init; } = new v0_3_2.WorkStreamSettingsModel();

        public DisplaySettingsModel DisplaySettings { get; init; } = new DisplaySettingsModel();

        public GraphCompilationModel GraphCompilation { get; init; } = new GraphCompilationModel();

        public ArrowGraphModel ArrowGraph { get; init; } = new ArrowGraphModel();

        public bool HasStaleOutputs { get; init; }
    }
}
