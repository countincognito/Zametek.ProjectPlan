namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record DiagramArrowGraphModel
    {
        public List<DiagramEdgeModel> Edges { get; init; } = new List<DiagramEdgeModel>();

        public List<DiagramNodeModel> Nodes { get; init; } = new List<DiagramNodeModel>();
    }
}
