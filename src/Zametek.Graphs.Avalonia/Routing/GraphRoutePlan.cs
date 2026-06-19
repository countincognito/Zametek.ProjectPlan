namespace Zametek.Graphs.Avalonia
{
    // A fully-resolved rectilinear route: the per-endpoint connection axes plus, for the detour shapes,
    // the position(s) the resolver slid the route to so it clears the nodes in its way. Carried from the
    // clash resolver onto the edge so the drawn geometry and the clearance check are built from exactly
    // the same numbers (see GraphEdgeGeometry.RouteCorners).
    //   - Direct: Source/Target axes; Primary = optional Z corner (null = midpoint). Secondary unused.
    //   - Bracket: Source == Target (the shared axis); Primary = the cross-leg coordinate (may be
    //     outside the endpoint span). Secondary unused.
    //   - Saucepan: BowlVertical chooses the dip direction (false = a horizontal bowl with vertical arms,
    //     dipping in Y; true = the transpose). Primary = the bowl's cross-leg coordinate. Each of
    //     Source/Target is a handled end when its axis is perpendicular to the arms (H for a horizontal
    //     bowl, V for a vertical bowl) and a direct end otherwise - so the axis pair selects no/one/two
    //     handles. Secondary = the handle stub length (null = a default half-node stub).
    public readonly record struct GraphRoutePlan(
        GraphConnectionAxis Source,
        GraphConnectionAxis Target,
        GraphRouteShape Shape = GraphRouteShape.Direct,
        double? Primary = null,
        double? Secondary = null,
        bool BowlVertical = false);
}
