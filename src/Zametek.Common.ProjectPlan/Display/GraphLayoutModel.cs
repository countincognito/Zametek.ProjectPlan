namespace Zametek.Common.ProjectPlan
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

    [Serializable]
    public record GraphNodeLayoutModel
    {
        public int Id { get; init; }

        public double X { get; init; }

        public double Y { get; init; }

        public double Width { get; init; }

        public double Height { get; init; }

        public string Label { get; init; } = string.Empty;

        public string? Name { get; init; }

        public string? Tooltip { get; init; }

        public string? FillColorHexCode { get; init; }

        public string? BorderColorHexCode { get; init; }

        public double BorderThickness { get; init; }

        public bool IsDashed { get; init; }
    }

    [Serializable]
    public record GraphEdgeLayoutModel
    {
        public int Id { get; init; }

        public int SourceId { get; init; }

        public int TargetId { get; init; }

        public double StrokeThickness { get; init; }

        public bool IsDashed { get; init; }
    }
}
