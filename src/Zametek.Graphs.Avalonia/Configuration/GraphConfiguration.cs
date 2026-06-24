namespace Zametek.Graphs.Avalonia
{
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
}
