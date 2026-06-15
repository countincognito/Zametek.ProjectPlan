namespace Zametek.Graphs.ProjectPlan
{
    // A single node in a DiagramGraphModel: what to draw for one graph node, already resolved by the
    // application (presentation, label text and hover tooltip). The serializer turns it into an MSAGL
    // node and a GraphML/GraphViz node; coordinates (X/Y) are only meaningful in the GraphML output.
    [Serializable]
    public record DiagramNodeModel
    {
        public int Id { get; init; }

        public double X { get; init; }

        public double Y { get; init; }

        public double Height { get; init; }

        public double Width { get; init; }

        public string? FillColorHexCode { get; init; }

        public string? BorderColorHexCode { get; init; }

        public GraphDashStyle BorderDashStyle { get; init; }

        public double BorderThickness { get; init; }

        public string? Text { get; init; }

        public string? Name { get; init; }

        // Hover tooltip for the interactive control; carried through to GraphLayoutModel.
        public string? Tooltip { get; init; }
    }
}
