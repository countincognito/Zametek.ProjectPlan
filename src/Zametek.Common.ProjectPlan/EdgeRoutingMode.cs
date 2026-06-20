namespace Zametek.Common.ProjectPlan
{
    // The edge-routing strategy persisted with a scenario's graph display settings. Mirrors the
    // control library's GraphEdgeRoutingMode (one-for-one with Microsoft.Msagl's EdgeRoutingMode),
    // but lives in Common so the data contracts carry no dependency on the graph library; the
    // application maps between the two. Unset is the default for scenarios saved before the routing
    // mode was persisted - on load the application keeps the per-graph preset rather than overriding
    // it (arrow and vertex have different preset modes, so there is no single meaningful numeric
    // default).
    [Serializable]
    public enum EdgeRoutingMode
    {
        Unset = 0,
        Spline,
        SplineBundling,
        StraightLine,
        SugiyamaSplines,
        Rectilinear,
        RectilinearToCenter,
        None,
    }
}
