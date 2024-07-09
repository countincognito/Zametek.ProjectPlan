namespace Zametek.Data.ProjectPlan.v0_2_0
{
    [Serializable]
    public record GraphCompilationErrorsModel
    {
        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; init; }

        public List<v0_1_0.CircularDependencyModel> CircularDependencies { get; init; } = [];

        public List<int> MissingDependencies { get; init; } = [];
    }
}
