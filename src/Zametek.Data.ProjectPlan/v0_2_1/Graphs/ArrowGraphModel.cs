namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    public record ArrowGraphModel
    {
        public List<ActivityEdgeModel> Edges { get; init; } = new List<ActivityEdgeModel>();

        public List<v0_1_0.EventNodeModel> Nodes { get; init; } = new List<v0_1_0.EventNodeModel>();

        public bool IsStale { get; init; }
    }
}
