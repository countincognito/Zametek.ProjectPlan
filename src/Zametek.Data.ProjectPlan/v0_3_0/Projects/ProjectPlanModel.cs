namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record ProjectPlanModel
    {
        public string Version { get; } = Versions.v0_3_0;

        public DateTimeOffset ProjectStart { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public v0_1_0.ArrowGraphSettingsModel ArrowGraphSettings { get; init; } = new v0_1_0.ArrowGraphSettingsModel();

        public v0_1_0.ResourceSettingsModel ResourceSettings { get; init; } = new v0_1_0.ResourceSettingsModel();

        public GraphCompilationModel GraphCompilation { get; init; } = new GraphCompilationModel();

        public ArrowGraphModel ArrowGraph { get; init; } = new ArrowGraphModel();

        public bool HasStaleOutputs { get; init; }
    }
}
