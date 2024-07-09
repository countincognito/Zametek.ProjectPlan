namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record CircularDependencyModel
    {
        public List<int> Dependencies { get; init; } = [];
    }
}
