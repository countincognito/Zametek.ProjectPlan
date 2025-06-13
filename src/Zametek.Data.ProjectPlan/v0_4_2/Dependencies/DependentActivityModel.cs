namespace Zametek.Data.ProjectPlan.v0_4_2
{
    [Serializable]
    public record DependentActivityModel
    {
        public v0_4_0.ActivityModel Activity { get; init; } = new v0_4_0.ActivityModel();

        public List<int> Dependencies { get; init; } = [];

        public List<int> ManualDependencies { get; init; } = [];

        public List<int> ResourceDependencies { get; init; } = [];

        public List<int> Successors { get; init; } = [];
    }
}
