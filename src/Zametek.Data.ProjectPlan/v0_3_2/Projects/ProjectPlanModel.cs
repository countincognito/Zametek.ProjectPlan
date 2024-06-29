namespace Zametek.Data.ProjectPlan.v0_3_2
{
    [Serializable]
    public record ProjectPlanModel
    {
        public string Version { get; } = Versions.v0_3_2;

        public DateTimeOffset ProjectStart { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public v0_1_0.ArrowGraphSettingsModel ArrowGraphSettings { get; init; } = new v0_1_0.ArrowGraphSettingsModel();

        public ResourceSettingsModel ResourceSettings { get; init; } = new ResourceSettingsModel();

        public WorkStreamSettingsModel WorkStreamSettings { get; init; } = new WorkStreamSettingsModel();

        public GraphCompilationModel GraphCompilation { get; init; } = new GraphCompilationModel();

        public v0_3_0.ArrowGraphModel ArrowGraph { get; init; } = new v0_3_0.ArrowGraphModel();

        public bool HasStaleOutputs { get; init; }
    }
}
