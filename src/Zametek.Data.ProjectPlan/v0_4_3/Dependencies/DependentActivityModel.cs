namespace Zametek.Data.ProjectPlan.v0_4_3
{
    [Serializable]
    public record DependentActivityModel
    {
        public v0_4_0.ActivityModel Activity { get; init; } = new v0_4_0.ActivityModel();

        public List<int> Dependencies { get; init; } = [];

        public List<int> PlanningDependencies { get; init; } = [];

        public List<int> ResourceDependencies { get; init; } = [];

        public List<int> Successors { get; init; } = [];
    }
}
