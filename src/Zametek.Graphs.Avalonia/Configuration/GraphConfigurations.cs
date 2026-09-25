namespace Zametek.Graphs.Avalonia
{
    // The two ready-made configurations. The constants were established by trial and error against
    // the monospace node labels, so they are kept verbatim from the original per-graph serializers.
    public static class GraphConfigurations
    {
        // The label width factors were worked out for Consolas, and the labels are now drawn in the bundled
        // Cascadia Mono (see GraphFonts), whose characters are wider: 1200 font units against Consolas's
        // 1126, both at 2048 to the em. Scaling the factors by that ratio keeps the node labels the width
        // they were, and widens the edge label boxes, which the factor divides, to fit the wider text.
        private const double c_CascadiaMonoWidthCorrection = 1126.0 / 1200.0;

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
            FontName = GraphFonts.SvgLabelFontFamilies,
            NodeFontStyle = GraphNodeFontStyle.Regular,
            // 1 label line * 34 label width / 14.
            LabelWidthCorrectionFactor = 1.0 * 34.0 / 14.0 * c_CascadiaMonoWidthCorrection,
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
            FontName = GraphFonts.SvgLabelFontFamilies,
            NodeFontStyle = GraphNodeFontStyle.Bold,
            // 3 label lines * 30 label width / 11.5.
            LabelWidthCorrectionFactor = 3.0 * 30.0 / 11.5 * c_CascadiaMonoWidthCorrection,
            LabelHeightCorrectionFactor = 3.0,
            EdgeRoutingMode = GraphEdgeRoutingMode.Spline,
            EdgeLabelFontSize = 12.0,
            EdgeLabelHeight = 12.0,
            InteractiveLayoutScalingFactor = 2.5,
        };
    }
}
