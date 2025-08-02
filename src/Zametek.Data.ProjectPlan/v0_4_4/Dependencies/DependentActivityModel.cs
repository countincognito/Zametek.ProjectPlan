namespace Zametek.Data.ProjectPlan.v0_4_4
{
    [Serializable]
    public record DependentActivityModel
    {
        public ActivityModel Activity { get; init; } = new();

        public List<int> Dependencies { get; init; } = [];

        public List<int> PlanningDependencies { get; init; } = [];

        public List<int> ResourceDependencies { get; init; } = [];

        public List<int> Successors { get; init; } = [];
    }
}
