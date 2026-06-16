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
    // only reads SupportsShowNames, the serializer only reads the MSAGL layout tuning constants.
    public record GraphConfiguration
    {
        // Interactive behaviour: whether the graph surfaces a "show names" toggle.
        public bool SupportsShowNames { get; init; }

        // MSAGL layout tuning (consumed by GraphSerializer). The node box size, the label box width,
        // the Consolas font-metric correction factors and the routing strategy all differ between the
        // two graphs; everything else is identical and kept as serializer constants.
        public double SvgNodeWidth { get; init; }

        public double SvgNodeHeight { get; init; }

        public double SvgNodeLabelWidth { get; init; }

        public GraphNodeFontStyle NodeFontStyle { get; init; }

        public GraphEdgeRoutingMode EdgeRoutingMode { get; init; }

        public double ConsolasLabelWidthCorrectionFactor { get; init; }

        public double ConsolasLabelHeightCorrectionFactor { get; init; }

        // The uniform scale applied to the MSAGL layout so the small layout boxes become a
        // comfortable interactive size.
        public double InteractiveLayoutScale { get; init; }
    }

    // The two ready-made configurations. The constants were established by trial and error against
    // the Consolas node labels, so they are kept verbatim from the original per-graph serializers.
    public static class GraphConfigurations
    {
        // Arrow graph: event nodes with a single-line finish-time label and single-line activity edge
        // labels; supports the show-names toggle.
        public static GraphConfiguration Arrow { get; } = new()
        {
            SupportsShowNames = true,
            SvgNodeWidth = 40.0,
            SvgNodeHeight = 34.0,
            SvgNodeLabelWidth = 34.0,
            NodeFontStyle = GraphNodeFontStyle.Regular,
            EdgeRoutingMode = GraphEdgeRoutingMode.SugiyamaSplines,
            // 1 label line * 34 label width / 14.
            ConsolasLabelWidthCorrectionFactor = 1.0 * 34.0 / 14.0,
            ConsolasLabelHeightCorrectionFactor = 0.7,
            InteractiveLayoutScale = 1.5,
        };

        // Vertex graph: activity nodes with a three-line ES/Id/EF | LS/Dur/LF label box and no edge
        // labels; no show-names toggle.
        public static GraphConfiguration Vertex { get; } = new()
        {
            SupportsShowNames = false,
            SvgNodeWidth = 38.0,
            SvgNodeHeight = 30.0,
            SvgNodeLabelWidth = 30.0,
            NodeFontStyle = GraphNodeFontStyle.Bold,
            EdgeRoutingMode = GraphEdgeRoutingMode.Spline,
            // 3 label lines * 30 label width / 11.5.
            ConsolasLabelWidthCorrectionFactor = 3.0 * 30.0 / 11.5,
            ConsolasLabelHeightCorrectionFactor = 3.0,
            InteractiveLayoutScale = 2.5,
        };
    }
}
