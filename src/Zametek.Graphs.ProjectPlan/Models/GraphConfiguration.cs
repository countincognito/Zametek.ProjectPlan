namespace Zametek.Graphs.ProjectPlan
{
    // The MSAGL node-label font weight. Mapped to Microsoft.Msagl.Drawing.FontStyle inside the
    // serializer so the library's own configuration surface carries no MSAGL types.
    public enum GraphNodeFontStyle
    {
        Regular,
        Bold
    }

    // The MSAGL edge-routing strategy. Mapped to Microsoft.Msagl.Core.Routing.EdgeRoutingMode inside
    // the serializer so the library's own configuration surface carries no MSAGL types.
    public enum GraphEdgeRoutingMode
    {
        SugiyamaSplines,
        Spline
    }

    // The single per-graph settings bundle that lets one interactive view-model and one serializer
    // serve both the arrow and vertex graphs. A consumer passes the relevant preset (see
    // GraphConfigurations); each part is used or ignored as appropriate - the interactive view-model
    // only reads SupportsShowNames, the serializer reads the layout tuning.
    //
    // NOTE on naming: these layout values are NOT SVG-specific despite feeding the SVG export. The
    // serializer runs one MSAGL layout pass that produces both the SVG image and the interactive
    // GraphLayoutModel, so the node sizes, label sizes, font and routing all shape the on-screen
    // node sizes and positions too. The corner radius is the only purely-SVG detail (it just rounds
    // the SVG's corners; the interactive view and the export renderer use their own radius).
    public record GraphConfiguration
    {
        // Interactive behaviour: whether the graph surfaces a "show names" toggle.
        public bool SupportsShowNames { get; init; }

        // The node box passed to MSAGL. Its size becomes the interactive node size (x the scale) and
        // shifts every laid-out position, so it is shared by the SVG and interactive renderings.
        public double NodeWidth { get; init; }

        public double NodeHeight { get; init; }

        // Rounded-corner radius of the MSAGL node box. Purely an SVG-render detail - it does not
        // change the node's bounding box, and the interactive view/export renderer use their own.
        public double NodeCornerRadiusX { get; init; }

        public double NodeCornerRadiusY { get; init; }

        // The node label box width, the label font and weight, and the font-metric correction factors
        // (worked out by trial and error for the chosen monospace font). These size the MSAGL labels,
        // which reserve layout space and so also nudge the interactive positions.
        public double NodeLabelWidth { get; init; }

        public string FontName { get; init; } = string.Empty;

        public GraphNodeFontStyle NodeFontStyle { get; init; }

        public double LabelWidthCorrectionFactor { get; init; }

        public double LabelHeightCorrectionFactor { get; init; }

        // The MSAGL edge-routing strategy and the edge-label box size (the arrow graph has edge
        // labels; the vertex graph leaves them empty). Both affect the laid-out positions.
        public GraphEdgeRoutingMode EdgeRoutingMode { get; init; }

        public double EdgeLabelFontSize { get; init; }

        public double EdgeLabelHeight { get; init; }

        // The uniform scale applied to the MSAGL layout so the small layout boxes become a
        // comfortable interactive size.
        public double InteractiveLayoutScalingFactor { get; init; }
    }

    // The two ready-made configurations. The constants were established by trial and error against
    // the monospace node labels, so they are kept verbatim from the original per-graph serializers.
    public static class GraphConfigurations
    {
        // Arrow graph: event nodes with a single-line finish-time label and single-line activity edge
        // labels; supports the show-names toggle.
        public static GraphConfiguration Arrow { get; } = new()
        {
            SupportsShowNames = true,
            NodeWidth = 40.0,
            NodeHeight = 34.0,
            NodeCornerRadiusX = 3.0,
            NodeCornerRadiusY = 2.0,
            NodeLabelWidth = 34.0,
            FontName = @"Consolas",
            NodeFontStyle = GraphNodeFontStyle.Regular,
            // 1 label line * 34 label width / 14.
            LabelWidthCorrectionFactor = 1.0 * 34.0 / 14.0,
            LabelHeightCorrectionFactor = 0.7,
            EdgeRoutingMode = GraphEdgeRoutingMode.SugiyamaSplines,
            EdgeLabelFontSize = 12.0,
            EdgeLabelHeight = 12.0,
            InteractiveLayoutScalingFactor = 1.5,
        };

        // Vertex graph: activity nodes with a three-line ES/Id/EF | LS/Dur/LF label box and no edge
        // labels; no show-names toggle.
        public static GraphConfiguration Vertex { get; } = new()
        {
            SupportsShowNames = false,
            NodeWidth = 38.0,
            NodeHeight = 30.0,
            NodeCornerRadiusX = 3.0,
            NodeCornerRadiusY = 2.0,
            NodeLabelWidth = 30.0,
            FontName = @"Consolas",
            NodeFontStyle = GraphNodeFontStyle.Bold,
            // 3 label lines * 30 label width / 11.5.
            LabelWidthCorrectionFactor = 3.0 * 30.0 / 11.5,
            LabelHeightCorrectionFactor = 3.0,
            EdgeRoutingMode = GraphEdgeRoutingMode.Spline,
            EdgeLabelFontSize = 12.0,
            EdgeLabelHeight = 12.0,
            InteractiveLayoutScalingFactor = 2.5,
        };
    }
}
