namespace Zametek.Data.ProjectPlan.v0_3_1
{
    [Serializable]
    public record ProjectPlanModel
    {
        public string Version { get; } = Versions.v0_3_1;

        public DateTimeOffset ProjectStart { get; init; }

        public List<v0_3_0.DependentActivityModel> DependentActivities { get; init; } = [];

        public v0_1_0.ArrowGraphSettingsModel ArrowGraphSettings { get; init; } = new v0_1_0.ArrowGraphSettingsModel();

        public ResourceSettingsModel ResourceSettings { get; init; } = new ResourceSettingsModel();

        public GraphCompilationModel GraphCompilation { get; init; } = new GraphCompilationModel();

        public v0_3_0.ArrowGraphModel ArrowGraph { get; init; } = new v0_3_0.ArrowGraphModel();

        public bool HasStaleOutputs { get; init; }
    }
}
