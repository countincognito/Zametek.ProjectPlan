using Avalonia;

namespace Zametek.Graphs.ProjectPlan
{
    // One cubic-bezier piece of an interactive edge. Pieces are contiguous (each Start is the previous
    // End), so an edge is one or more of these chained together. A straight run is just a bezier whose
    // control points lie on its own chord.
    internal readonly record struct GraphEdgeSegment(Point Start, Point Control1, Point Control2, Point End);
}
