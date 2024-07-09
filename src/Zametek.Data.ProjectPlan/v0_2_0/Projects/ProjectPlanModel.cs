namespace Zametek.Data.ProjectPlan.v0_2_0
{
    [Serializable]
    public record ProjectPlanModel
    {
        public string Version { get; } = Versions.v0_2_0;

        public DateTime ProjectStart { get; init; }

        public List<v0_1_0.DependentActivityModel> DependentActivities { get; init; } = [];

        public v0_1_0.ArrowGraphSettingsModel? ArrowGraphSettings { get; init; }

        public v0_1_0.ResourceSettingsModel? ResourceSettings { get; init; }

        public GraphCompilationModel? GraphCompilation { get; init; }

        public v0_1_0.ArrowGraphModel? ArrowGraph { get; init; }

        public bool HasStaleOutputs { get; init; }
    }
}
