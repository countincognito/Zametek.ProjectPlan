namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record VertexGraphModel
    {
        public List<EventEdgeModel> Edges { get; init; } = new List<EventEdgeModel>();

        public List<ActivityNodeModel> Nodes { get; init; } = new List<ActivityNodeModel>();
    }
}
