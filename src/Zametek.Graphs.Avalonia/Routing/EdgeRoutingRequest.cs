namespace Zametek.Graphs.Avalonia
{
    // An immutable snapshot of everything needed to route the edges: the obstacle rectangles, the
    // edges and the routing mode, all in interactive (screen) coordinates. Captured on the UI thread so
    // the routing can run off-thread without reading live, mutating view-models.
    public sealed record EdgeRoutingRequest(
        IReadOnlyList<EdgeRoutingNode> Nodes,
        IReadOnlyList<EdgeRoutingEdge> Edges,
        GraphEdgeRoutingMode Mode);
}
