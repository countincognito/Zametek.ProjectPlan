namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectPlanModel
    {
        public string Version { get; init; } = string.Empty;

        public DateTimeOffset ProjectStart { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = new List<DependentActivityModel>();

        public ArrowGraphSettingsModel ArrowGraphSettings { get; init; } = new ArrowGraphSettingsModel();

        public ResourceSettingsModel ResourceSettings { get; init; } = new ResourceSettingsModel();

        public GraphCompilationModel GraphCompilation { get; init; } = new GraphCompilationModel();

        public ArrowGraphModel ArrowGraph { get; init; } = new ArrowGraphModel();

        public bool HasStaleOutputs { get; init; }
    }
}
