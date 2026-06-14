namespace Zametek.Graphs.ProjectPlan
{
    [Serializable]
    public record DiagramGraphModel
    {
        public List<DiagramEdgeModel> Edges { get; init; } = [];

        public List<DiagramNodeModel> Nodes { get; init; } = [];
    }
}
