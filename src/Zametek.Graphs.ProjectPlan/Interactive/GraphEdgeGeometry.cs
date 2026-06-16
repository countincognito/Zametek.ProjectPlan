using Avalonia;

namespace Zametek.Graphs.ProjectPlan
{
    // One cubic-bezier piece of an interactive edge. Pieces are contiguous (each Start is the previous
    // End), so an edge is one or more of these chained together. A straight run is just a bezier whose
    // control points lie on its own chord.
    internal readonly record struct GraphEdgeSegment(Point Start, Point Control1, Point Control2, Point End);

    // Computes the on-screen shape of an interactive edge from a GraphEdgeRoutingMode, client-side (no
    // MSAGL) so it recomputes live as nodes are dragged. The shape is returned as a list of contiguous
    // cubic-bezier segments from the (already border-clipped) start to end. The serializer lays the
    // graph out left-to-right (LayerDirection.LR), so the shapes are built around that primary axis:
    //
    //   - Spline / SugiyamaSplines / SplineBundling -> a single smooth horizontal connector: both
    //     control points sit at the horizontal midpoint, each at its own endpoint's height. Endpoints
    //     on the same level (equal Y) leave the controls on the chord, giving a straight line; a
    //     vertical offset gives a smooth S-curve whose direction follows the offset (so edges going up
    //     and edges going down bend opposite ways, not all to one side).
    //   - StraightLine / None / anything else -> a single straight segment.
    //   - Rectilinear / RectilinearToCenter -> an orthogonal "Z" path with right-angle corners: leave
    //     horizontally, turn vertically at the midpoint, arrive horizontally (three straight segments).
    //
    // Both the interactive view-model (an Avalonia Geometry) and the export renderer (a SkiaSharp path)
    // build from these same segments, so the on-screen and exported edges stay identical.
    //
    // This is a deliberately coarse client-side approximation of MSAGL's routing - it does NOT avoid
    // obstacles, cannot truly bundle edges, and several MSAGL modes collapse to the same local shape
    // (SplineBundling draws as a plain Spline; None draws as a straight line so the connection stays
    // visible). The fixed-layout SVG export routes through MSAGL itself, so it honours each mode fully.
    // To mirror a different Microsoft.Msagl.Core.Routing.EdgeRoutingMode value, add it to
    // GraphEdgeRoutingMode and give it an arm in BuildSegments.
    internal static class GraphEdgeGeometry
    {
        public static IReadOnlyList<GraphEdgeSegment> BuildSegments(
            GraphEdgeRoutingMode routingMode,
            Point start,
            Point end)
        {
            return routingMode switch
            {
                GraphEdgeRoutingMode.Rectilinear or GraphEdgeRoutingMode.RectilinearToCenter
                    => OrthogonalSegments(start, end),
                GraphEdgeRoutingMode.Spline
                    or GraphEdgeRoutingMode.SugiyamaSplines
                    or GraphEdgeRoutingMode.SplineBundling
                    => [HorizontalConnector(start, end)],
                // StraightLine, None and any future value: a straight line.
                _ => [StraightSegment(start, end)],
            };
        }

        // The point on a single cubic-bezier segment at parameter t.
        public static Point PointOnCubic(Point start, Point control1, Point control2, Point end, double t)
        {
            double u = 1.0 - t;
            double w0 = u * u * u;
            double w1 = 3.0 * u * u * t;
            double w2 = 3.0 * u * t * t;
            double w3 = t * t * t;
            return new Point(
                (w0 * start.X) + (w1 * control1.X) + (w2 * control2.X) + (w3 * end.X),
                (w0 * start.Y) + (w1 * control1.Y) + (w2 * control2.Y) + (w3 * end.Y));
        }

        // The geometric middle of the path (the midpoint of the middle segment), used to anchor the
        // edge label. For a single-segment edge this is just t = 0.5; for the three-segment orthogonal
        // path it lands on the middle (vertical) run.
        public static Point Midpoint(IReadOnlyList<GraphEdgeSegment> segments)
        {
            GraphEdgeSegment mid = segments[segments.Count / 2];
            return PointOnCubic(mid.Start, mid.Control1, mid.Control2, mid.End, 0.5);
        }

        // Both control points at the horizontal midpoint, each at its endpoint's height: a straight
        // line when the endpoints are level, otherwise a smooth S-curve toward the offset.
        private static GraphEdgeSegment HorizontalConnector(Point start, Point end)
        {
            double midX = (start.X + end.X) / 2.0;
            return new GraphEdgeSegment(
                start,
                new Point(midX, start.Y),
                new Point(midX, end.Y),
                end);
        }

        // An orthogonal "Z": horizontal out of the source, vertical at the midpoint, horizontal into
        // the target. Collapses to a single straight run when the endpoints already share an axis (so
        // there is no zero-length corner).
        private static IReadOnlyList<GraphEdgeSegment> OrthogonalSegments(Point start, Point end)
        {
            const double epsilon = 1e-6;
            if (Math.Abs(end.Y - start.Y) < epsilon || Math.Abs(end.X - start.X) < epsilon)
            {
                return [StraightSegment(start, end)];
            }

            double midX = (start.X + end.X) / 2.0;
            var corner1 = new Point(midX, start.Y);
            var corner2 = new Point(midX, end.Y);
            return
            [
                StraightSegment(start, corner1),
                StraightSegment(corner1, corner2),
                StraightSegment(corner2, end),
            ];
        }

        // A straight line expressed as a bezier: control points at one-third and two-thirds along the
        // chord.
        private static GraphEdgeSegment StraightSegment(Point start, Point end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            return new GraphEdgeSegment(
                start,
                new Point(start.X + (dx / 3.0), start.Y + (dy / 3.0)),
                new Point(start.X + (2.0 * dx / 3.0), start.Y + (2.0 * dy / 3.0)),
                end);
        }
    }
}
