using Avalonia;

namespace Zametek.Graphs.ProjectPlan
{
    // One cubic-bezier piece of an interactive edge. Pieces are contiguous (each Start is the previous
    // End), so an edge is one or more of these chained together. A straight run is just a bezier whose
    // control points lie on its own chord.
    internal readonly record struct GraphEdgeSegment(Point Start, Point Control1, Point Control2, Point End);

    // Which axis an interactive edge leaves the source / enters the target along: Horizontal = the
    // left/right node sides, Vertical = the top/bottom sides. Chosen per endpoint (see the interactive
    // view-model's hybrid resolve) and fed into the spline/rectilinear builders so a vertically-stacked
    // arrangement connects top-to-bottom instead of always sideways.
    internal enum GraphConnectionAxis
    {
        Horizontal,
        Vertical,
    }

    // Computes the on-screen shape of an interactive edge from a GraphEdgeRoutingMode, client-side (no
    // MSAGL) so it recomputes live as nodes are dragged. The shape is returned as a list of contiguous
    // cubic-bezier segments from the (already border-clipped) start to end. The serializer lays the
    // graph out left-to-right (LayerDirection.LR), so the shapes are built around that primary axis:
    //
    //   - Spline / SugiyamaSplines / SplineBundling -> a single smooth connector whose control points
    //     follow the per-endpoint GraphConnectionAxis: a horizontal endpoint puts its control at the
    //     horizontal midpoint at its own height, a vertical endpoint at the vertical midpoint at its
    //     own X. Endpoints aligned along the chosen axis leave the controls on the chord, giving a
    //     straight line; an offset gives a smooth S-curve (or a mixed curve when the two axes differ).
    //   - StraightLine / None / anything else -> a single straight segment.
    //   - Rectilinear / RectilinearToCenter -> an orthogonal path with right-angle corners, shaped by
    //     the per-endpoint axes: matching axes give a three-segment "Z" (corners at the horizontal or
    //     vertical midpoint), differing axes give a two-segment "L" with a single corner.
    //
    // The connection axes are supplied by the caller (the interactive view-model resolves them from the
    // pre-drag MSAGL route plus the current arrangement), so the same arrangement can connect side-to-
    // side or top-to-bottom depending on what the layout favours.
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
            Point end,
            GraphConnectionAxis sourceAxis,
            GraphConnectionAxis targetAxis)
        {
            return routingMode switch
            {
                GraphEdgeRoutingMode.Rectilinear or GraphEdgeRoutingMode.RectilinearToCenter
                    => OrthogonalSegments(start, end, sourceAxis, targetAxis),
                GraphEdgeRoutingMode.Spline
                    or GraphEdgeRoutingMode.SugiyamaSplines
                    or GraphEdgeRoutingMode.SplineBundling
                    => [Connector(start, end, sourceAxis, targetAxis)],
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

        // The point a given distance of travel back from the path's end (the tip), found by walking
        // backward along the segments and sampling each cubic, crossing into earlier segments when the
        // final leg is shorter than the span. Used to aim the arrowhead along a meaningful stretch of
        // the curve near the tip rather than its final control leg, which for the spline/rectilinear
        // approximations is a tiny horizontally-pinned nub that would otherwise snap the head sideways
        // on a near-vertical edge. Falls back to the path's start when the whole path is shorter than
        // the span.
        public static Point AnchorBeforeEnd(IReadOnlyList<GraphEdgeSegment> segments, double span)
        {
            const int samplesPerSegment = 16;
            Point previous = segments[^1].End;
            double travelled = 0.0;

            // Walk segments tip-to-start; within each, sample from just before the end (t just under
            // 1) down to its start (t = 0). Points are contiguous, so 'previous' carries across the
            // segment boundary without double-counting the shared point.
            for (int s = segments.Count - 1; s >= 0; s--)
            {
                GraphEdgeSegment segment = segments[s];
                for (int i = 1; i <= samplesPerSegment; i++)
                {
                    double t = 1.0 - ((double)i / samplesPerSegment);
                    Point sample = PointOnCubic(segment.Start, segment.Control1, segment.Control2, segment.End, t);
                    double dx = sample.X - previous.X;
                    double dy = sample.Y - previous.Y;
                    travelled += Math.Sqrt((dx * dx) + (dy * dy));
                    if (travelled >= span)
                    {
                        return sample;
                    }
                    previous = sample;
                }
            }

            return segments[0].Start;
        }

        // The geometric middle of the path (the midpoint of the middle segment), used to anchor the
        // edge label. For a single-segment edge this is just t = 0.5; for the three-segment orthogonal
        // path it lands on the middle (vertical) run.
        public static Point Midpoint(IReadOnlyList<GraphEdgeSegment> segments)
        {
            GraphEdgeSegment mid = segments[segments.Count / 2];
            return PointOnCubic(mid.Start, mid.Control1, mid.Control2, mid.End, 0.5);
        }

        // Whether a mode shapes its edges around the connection axes (the spline and rectilinear
        // families) or ignores them (StraightLine / None, which just draw the centre-to-centre line).
        internal static bool UsesConnectionAxes(GraphEdgeRoutingMode routingMode)
        {
            return routingMode switch
            {
                GraphEdgeRoutingMode.StraightLine or GraphEdgeRoutingMode.None => false,
                _ => true,
            };
        }

        // Classify a span as a horizontal or vertical connection: vertical when the vertical span
        // dominates, horizontal otherwise (ties resolve to horizontal).
        internal static GraphConnectionAxis ClassifyAxis(double dx, double dy)
        {
            return dy > dx ? GraphConnectionAxis.Vertical : GraphConnectionAxis.Horizontal;
        }

        // The axis a routed edge leaves its source along, taken from the first segment's start tangent
        // (Control1 - Start), falling back to the segment chord if that tangent is degenerate.
        internal static GraphConnectionAxis ExitAxis(GraphEdgeSegment first)
        {
            double dx = Math.Abs(first.Control1.X - first.Start.X);
            double dy = Math.Abs(first.Control1.Y - first.Start.Y);
            if (dx < 1e-6 && dy < 1e-6)
            {
                dx = Math.Abs(first.End.X - first.Start.X);
                dy = Math.Abs(first.End.Y - first.Start.Y);
            }
            return ClassifyAxis(dx, dy);
        }

        // The axis a routed edge enters its target along, taken from the last segment's end tangent
        // (End - Control2), falling back to the segment chord if that tangent is degenerate.
        internal static GraphConnectionAxis EntryAxis(GraphEdgeSegment last)
        {
            double dx = Math.Abs(last.End.X - last.Control2.X);
            double dy = Math.Abs(last.End.Y - last.Control2.Y);
            if (dx < 1e-6 && dy < 1e-6)
            {
                dx = Math.Abs(last.End.X - last.Start.X);
                dy = Math.Abs(last.End.Y - last.Start.Y);
            }
            return ClassifyAxis(dx, dy);
        }

        // The hybrid axis choice: keep the captured (pre-drag) axis unless the current arrangement
        // exceeds the flip ratio against it, in which case fall back to the dominant axis. With nothing
        // captured, the dominant axis is used outright. flipRatio > 1 gives a dead-band around 45
        // degrees so the orientation does not flip-flop.
        internal static GraphConnectionAxis ResolveAxis(
            GraphConnectionAxis? captured,
            GraphConnectionAxis dominant,
            double dx,
            double dy,
            double flipRatio)
        {
            if (captured is not GraphConnectionAxis axis)
            {
                return dominant;
            }
            if (axis == GraphConnectionAxis.Horizontal && dy > flipRatio * dx)
            {
                return GraphConnectionAxis.Vertical;
            }
            if (axis == GraphConnectionAxis.Vertical && dx > flipRatio * dy)
            {
                return GraphConnectionAxis.Horizontal;
            }
            return axis;
        }

        // The centre of the node side the edge attaches to for the given axis: the left/right-centre for
        // a horizontal connection, the top/bottom-centre for a vertical one, choosing the side that
        // faces the other node (toward). This matches MSAGL's edge ports, so the approximation meets the
        // node where the settled route does.
        internal static Point AttachPoint(Point centre, double width, double height, GraphConnectionAxis axis, Point toward)
        {
            if (axis == GraphConnectionAxis.Horizontal)
            {
                double x = toward.X >= centre.X ? centre.X + (width / 2.0) : centre.X - (width / 2.0);
                return new Point(x, centre.Y);
            }
            double y = toward.Y >= centre.Y ? centre.Y + (height / 2.0) : centre.Y - (height / 2.0);
            return new Point(centre.X, y);
        }

        // A single smooth cubic whose control points follow the per-endpoint axis: a horizontal
        // endpoint anchors its control at the horizontal midpoint (keeping its own height), a vertical
        // endpoint at the vertical midpoint (keeping its own X). Matching axes give a straight line when
        // the endpoints are aligned along that axis, otherwise a smooth S-curve; differing axes give a
        // smooth curve that leaves along one axis and arrives along the other.
        private static GraphEdgeSegment Connector(
            Point start,
            Point end,
            GraphConnectionAxis sourceAxis,
            GraphConnectionAxis targetAxis)
        {
            double midX = (start.X + end.X) / 2.0;
            double midY = (start.Y + end.Y) / 2.0;
            Point control1 = sourceAxis == GraphConnectionAxis.Horizontal
                ? new Point(midX, start.Y)
                : new Point(start.X, midY);
            Point control2 = targetAxis == GraphConnectionAxis.Horizontal
                ? new Point(midX, end.Y)
                : new Point(end.X, midY);
            return new GraphEdgeSegment(start, control1, control2, end);
        }

        // An orthogonal path shaped by the per-endpoint axes. Matching axes give a three-segment "Z"
        // (corners at the horizontal midpoint for two horizontal ends, the vertical midpoint for two
        // vertical ends); differing axes give a two-segment "L" with a single corner (leave along the
        // source axis, arrive along the target axis). Collapses to a single straight run when the
        // endpoints already share an axis (so there is no zero-length corner).
        private static IReadOnlyList<GraphEdgeSegment> OrthogonalSegments(
            Point start,
            Point end,
            GraphConnectionAxis sourceAxis,
            GraphConnectionAxis targetAxis)
        {
            const double epsilon = 1e-6;
            if (Math.Abs(end.Y - start.Y) < epsilon || Math.Abs(end.X - start.X) < epsilon)
            {
                return [StraightSegment(start, end)];
            }

            double midX = (start.X + end.X) / 2.0;
            double midY = (start.Y + end.Y) / 2.0;

            if (sourceAxis == GraphConnectionAxis.Horizontal && targetAxis == GraphConnectionAxis.Horizontal)
            {
                var corner1 = new Point(midX, start.Y);
                var corner2 = new Point(midX, end.Y);
                return
                [
                    StraightSegment(start, corner1),
                    StraightSegment(corner1, corner2),
                    StraightSegment(corner2, end),
                ];
            }

            if (sourceAxis == GraphConnectionAxis.Vertical && targetAxis == GraphConnectionAxis.Vertical)
            {
                var corner1 = new Point(start.X, midY);
                var corner2 = new Point(end.X, midY);
                return
                [
                    StraightSegment(start, corner1),
                    StraightSegment(corner1, corner2),
                    StraightSegment(corner2, end),
                ];
            }

            if (sourceAxis == GraphConnectionAxis.Horizontal)
            {
                // Leave horizontally, arrive vertically: corner level with the source, above/below the
                // target.
                var corner = new Point(end.X, start.Y);
                return [StraightSegment(start, corner), StraightSegment(corner, end)];
            }

            // Leave vertically, arrive horizontally: corner above/below the source, level with the
            // target.
            var elbow = new Point(start.X, end.Y);
            return [StraightSegment(start, elbow), StraightSegment(elbow, end)];
        }

        // A straight line expressed as a bezier: control points at one-third and two-thirds along the
        // chord. Shared with the MSAGL router, which builds straight runs the same way.
        internal static GraphEdgeSegment StraightSegment(Point start, Point end)
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
