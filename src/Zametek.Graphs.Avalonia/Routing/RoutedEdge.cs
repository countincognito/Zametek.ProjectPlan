namespace Zametek.Graphs.Avalonia
{
    // The routed geometry for one edge, as contiguous cubic-bezier segments in the same (screen)
    // coordinates as the request.
    public readonly record struct RoutedEdge(int Id, IReadOnlyList<GraphEdgeSegment> Segments);
}
