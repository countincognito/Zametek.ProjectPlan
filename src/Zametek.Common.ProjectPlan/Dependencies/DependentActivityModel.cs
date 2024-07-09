namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record DependentActivityModel
    {
        public ActivityModel Activity { get; init; } = new ActivityModel();

        public List<int> Dependencies { get; init; } = [];

        public List<int> ResourceDependencies { get; init; } = [];
    }
}
