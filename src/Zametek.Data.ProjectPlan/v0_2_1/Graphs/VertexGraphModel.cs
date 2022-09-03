namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    public record VertexGraphModel
    {
        public List<v0_1_0.EventEdgeModel> Edges { get; init; } = new List<v0_1_0.EventEdgeModel>();

        public List<ActivityNodeModel> Nodes { get; init; } = new List<ActivityNodeModel>();
    }
}
