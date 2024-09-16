namespace Zametek.Data.ProjectPlan.v0_4_0
{
    [Serializable]
    public record ArrowGraphModel
    {
        public List<ActivityEdgeModel> Edges { get; init; } = [];

        public List<v0_3_0.EventNodeModel> Nodes { get; init; } = [];

        public bool IsStale { get; init; }
    }
}
