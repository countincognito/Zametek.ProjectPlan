using System.Threading;

namespace Zametek.Graphs.Avalonia
{
    // Routes interactive edges with the real MSAGL routing algorithms for the supplied (possibly
    // dragged) node positions, off the UI thread. The call is the seam shared by B' and B: B' invokes
    // it once whenever the arrangement settles (initial layout / reset / mode change / drag-end); B
    // would invoke it continuously while dragging (throttled) and could supply a persistent
    // implementation behind this same interface that keeps a visibility graph and reroutes only the
    // dragged node's incident edges. Implementations must honour the cancellation token so a newer
    // request supersedes an in-flight one.
    public interface IInteractiveEdgeRouter
    {
        Task<IReadOnlyList<RoutedEdge>> RouteAsync(EdgeRoutingRequest request, CancellationToken cancellationToken);
    }
}
