namespace Zametek.Graphs.ProjectPlan
{
    // A single edge in a DiagramGraphModel: what to draw for one graph edge, already resolved by the
    // application (presentation, label text/visibility and hover tooltip). The serializer turns it
    // into an MSAGL edge and a GraphML/GraphViz edge.
    [Serializable]
    public record DiagramEdgeModel
    {
        public int Id { get; init; }

        public string? Name { get; init; }

        public int SourceId { get; init; }

        public int TargetId { get; init; }

        public GraphDashStyle DashStyle { get; init; }

        public string? ForegroundColorHexCode { get; init; }

        public double StrokeThickness { get; init; }

        public string? Label { get; init; }

        public bool ShowLabel { get; init; }

        // Hover tooltip for the interactive control; carried through to GraphLayoutModel.
        public string? Tooltip { get; init; }
    }
}
