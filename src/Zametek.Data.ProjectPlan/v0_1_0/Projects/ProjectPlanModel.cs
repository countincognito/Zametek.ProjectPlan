namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record ProjectPlanModel
    {
        public string Version { get; } = Versions.v0_1_0;

        public DateTime ProjectStart { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public ArrowGraphSettingsModel? ArrowGraphSettings { get; init; }

        public ResourceSettingsModel? ResourceSettings { get; init; }

        public GraphCompilationModel? GraphCompilation { get; init; }

        public ArrowGraphModel? ArrowGraph { get; init; }

        public bool HasStaleOutputs { get; init; }
    }
}
