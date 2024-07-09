namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    public record ArrowGraphModel
    {
        public List<ActivityEdgeModel> Edges { get; init; } = [];

        public List<v0_1_0.EventNodeModel> Nodes { get; init; } = [];

        public bool IsStale { get; init; }
    }
}
