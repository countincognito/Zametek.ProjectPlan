namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record GraphCompilationModel
    {
        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; init; }

        public List<CircularDependencyModel> CircularDependencies { get; init; } = [];

        public List<int> MissingDependencies { get; init; } = [];

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public List<ResourceScheduleModel> ResourceSchedules { get; init; } = [];

        public int CyclomaticComplexity { get; init; }

        public int Duration { get; init; }
    }
}
