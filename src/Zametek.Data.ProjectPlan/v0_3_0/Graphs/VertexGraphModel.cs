namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record VertexGraphModel
    {
        public List<EventEdgeModel> Edges { get; init; } = [];

        public List<ActivityNodeModel> Nodes { get; init; } = [];
    }
}
