namespace Zametek.Graphs.ProjectPlan
{
    // The edge-routing strategy. These mirror Microsoft.Msagl.Core.Routing.EdgeRoutingMode one-for-one
    // (mapped inside the serializer, so the library's own configuration surface carries no MSAGL
    // types), and they also drive the interactive view's client-side edge shape (see
    // GraphEdgeGeometry): spline modes draw a smooth connector, straight modes a line, rectilinear
    // modes an orthogonal right-angle path. The fixed-layout SVG export routes through MSAGL itself,
    // so it honours each mode fully; the interactive view is a coarse local approximation (no obstacle
    // avoidance, no edge bundling). Note that None and SplineBundling are passed straight through to
    // MSAGL for the SVG export and may need extra MSAGL settings there; the shipped presets stay on
    // the well-trodden Spline/SugiyamaSplines, so the other modes are opt-in for consumers.
    public enum GraphEdgeRoutingMode
    {
        Spline,
        SplineBundling,
        StraightLine,
        SugiyamaSplines,
        Rectilinear,
        RectilinearToCenter,
        None
    }
}
