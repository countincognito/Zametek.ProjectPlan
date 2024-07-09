namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record DependentActivityModel
    {
        public ActivityModel Activity { get; init; } = new ActivityModel();

        public List<int> Dependencies { get; init; } = [];

        public List<int> ResourceDependencies { get; init; } = [];
    }
}
