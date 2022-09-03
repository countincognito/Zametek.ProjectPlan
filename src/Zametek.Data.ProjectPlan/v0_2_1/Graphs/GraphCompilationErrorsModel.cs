namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    public record GraphCompilationErrorsModel
    {
        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; init; }

        public List<v0_1_0.CircularDependencyModel> CircularDependencies { get; init; } = new List<v0_1_0.CircularDependencyModel>();

        public List<int> MissingDependencies { get; init; } = new List<int>();

        public List<int> InvalidConstraints { get; init; } = new List<int>();
    }
}
