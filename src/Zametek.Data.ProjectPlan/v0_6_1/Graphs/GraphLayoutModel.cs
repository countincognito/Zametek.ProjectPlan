namespace Zametek.Data.ProjectPlan.v0_6_1
{
    [Serializable]
    public record GraphLayoutModel
    {
        public List<NodeLayoutModel> Nodes { get; init; } = [];
    }
}
