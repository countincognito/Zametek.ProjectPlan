namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record DiagramVertexGraphModel
    {
        public List<DiagramEdgeModel> Edges { get; init; } = [];

        public List<DiagramNodeModel> Nodes { get; init; } = [];
    }
}
