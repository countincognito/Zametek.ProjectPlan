namespace Zametek.Graphs.ProjectPlan
{
    // The *input* to a layout/serialization pass: a flat, coordinate-free description of the graph
    // to be drawn - nodes and edges already carrying their resolved presentation (fill/border
    // colour, dash style, thickness) and finished label text. It is what the application produces
    // from its domain graph and hands to the serializers; the serializers feed it to MSAGL (for
    // SVG and the interactive layout) and to the GraphML/GraphViz writers. It says *what* to draw,
    // not *where* - there are no positions here.
    //
    // Contrast with GraphLayoutModel, which is the *output* of the MSAGL layout pass and carries the
    // resolved on-screen coordinates the interactive control places real Avalonia controls at.
    [Serializable]
    public record DiagramGraphModel
    {
        public List<DiagramEdgeModel> Edges { get; init; } = [];

        public List<DiagramNodeModel> Nodes { get; init; } = [];
    }
}
