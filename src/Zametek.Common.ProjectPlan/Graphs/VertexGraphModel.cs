namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record VertexGraphModel
    {
        public List<EventEdgeModel> Edges { get; init; } = [];

        public List<ActivityNodeModel> Nodes { get; init; } = [];
    }
}
