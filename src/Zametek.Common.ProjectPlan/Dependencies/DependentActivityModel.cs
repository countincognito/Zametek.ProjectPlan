namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record DependentActivityModel
    {
        public ActivityModel Activity { get; init; } = new ActivityModel();

        public List<int> Dependencies { get; init; } = new List<int>();

        public List<int> ResourceDependencies { get; init; } = new List<int>();
    }
}
