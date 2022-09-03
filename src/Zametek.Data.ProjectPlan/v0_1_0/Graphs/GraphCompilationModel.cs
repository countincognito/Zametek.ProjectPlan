namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record GraphCompilationModel
    {
        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; init; }

        public List<CircularDependencyModel> CircularDependencies { get; init; } = new List<CircularDependencyModel>();

        public List<int> MissingDependencies { get; init; } = new List<int>();

        public List<DependentActivityModel> DependentActivities { get; init; } = new List<DependentActivityModel>();

        public List<ResourceScheduleModel> ResourceSchedules { get; init; } = new List<ResourceScheduleModel>();

        public int CyclomaticComplexity { get; init; }

        public int Duration { get; init; }
    }
}
