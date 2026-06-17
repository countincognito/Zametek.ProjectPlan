using System.Threading;

namespace Zametek.Graphs.ProjectPlan
{
    // An obstacle node in a routing request: its rectangle in interactive (screen) coordinates.
    internal readonly record struct EdgeRoutingNode(int Id, double X, double Y, double Width, double Height);

    // An edge to route, identified by its endpoints' node ids.
    internal readonly record struct EdgeRoutingEdge(int Id, int SourceId, int TargetId);

    // An immutable snapshot of everything needed to route the edges: the obstacle rectangles, the
    // edges and the routing mode, all in interactive (screen) coordinates. Captured on the UI thread so
    // the routing can run off-thread without reading live, mutating view-models.
    internal sealed record EdgeRoutingRequest(
        IReadOnlyList<EdgeRoutingNode> Nodes,
        IReadOnlyList<EdgeRoutingEdge> Edges,
        GraphEdgeRoutingMode Mode);

    // The routed geometry for one edge, as contiguous cubic-bezier segments in the same (screen)
    // coordinates as the request.
    internal readonly record struct RoutedEdge(int Id, IReadOnlyList<GraphEdgeSegment> Segments);

    // Routes interactive edges with the real MSAGL routing algorithms for the supplied (possibly
    // dragged) node positions, off the UI thread. The call is the seam shared by B' and B: B' invokes
    // it once whenever the arrangement settles (initial layout / reset / mode change / drag-end); B
    // would invoke it continuously while dragging (throttled) and could supply a persistent
    // implementation behind this same interface that keeps a visibility graph and reroutes only the
    // dragged node's incident edges. Implementations must honour the cancellation token so a newer
    // request supersedes an in-flight one.
    internal interface IInteractiveEdgeRouter
    {
        Task<IReadOnlyList<RoutedEdge>> RouteAsync(EdgeRoutingRequest request, CancellationToken cancellationToken);
    }
}
