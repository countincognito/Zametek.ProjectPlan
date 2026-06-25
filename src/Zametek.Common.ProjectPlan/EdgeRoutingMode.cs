namespace Zametek.Common.ProjectPlan
{
    // The edge-routing strategy persisted with a scenario's graph display settings. Mirrors the
    // control library's GraphEdgeRoutingMode (mapped by name, so the data contracts carry no
    // dependency on the graph library). None is the zero value, so it is the default for a scenario
    // that has not stored a mode (e.g. a new project); the application applies it to both graphs.
    [Serializable]
    public enum EdgeRoutingMode
    {
        None = 0,
        Spline,
        SplineBundling,
        StraightLine,
        SugiyamaSplines,
        Rectilinear,
        RectilinearToCenter,
    }
}
