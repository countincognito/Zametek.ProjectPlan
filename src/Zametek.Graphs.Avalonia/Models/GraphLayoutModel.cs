namespace Zametek.Graphs.Avalonia
{
    // The *output* of the MSAGL layout pass: view-agnostic, laid-out graph geometry. Where
    // DiagramGraphModel is the coordinate-free *input* that says what to draw (and feeds the
    // SVG/GraphML/GraphViz serializers), GraphLayoutModel is what comes back out once MSAGL has
    // placed everything - it carries resolved on-screen coordinates and sizes so the interactive
    // control can position real Avalonia controls for each node and draw live edges between them.
    // Coordinates are top-left origin, Y increasing downward. Presentation already resolved on the
    // diagram is copied through (colours/dash/thickness/labels/tooltips) so the control needs
    // nothing from the domain models.
    [Serializable]
    public record GraphLayoutModel
    {
        public double Width { get; init; }

        public double Height { get; init; }

        public List<GraphNodeLayoutModel> Nodes { get; init; } = [];

        public List<GraphEdgeLayoutModel> Edges { get; init; } = [];
    }
}
