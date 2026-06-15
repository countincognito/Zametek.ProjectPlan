namespace Zametek.Graphs.ProjectPlan
{
    // View-agnostic, laid-out graph geometry produced from the MSAGL layout pass.
    // Unlike DiagramGraphModel (which feeds the SVG/GraphML/GraphViz serializers),
    // this carries resolved on-screen coordinates so an interactive control can place
    // real Avalonia controls. Coordinates are top-left origin, Y increasing downward.
    [Serializable]
    public record GraphLayoutModel
    {
        public double Width { get; init; }

        public double Height { get; init; }

        public List<GraphNodeLayoutModel> Nodes { get; init; } = [];

        public List<GraphEdgeLayoutModel> Edges { get; init; } = [];
    }
}
