namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record DependentActivityModel
    {
        public ActivityModel? Activity { get; init; }

        public List<int> Dependencies { get; init; } = new List<int>();

        public List<int> ResourceDependencies { get; init; } = new List<int>();
    }
}
