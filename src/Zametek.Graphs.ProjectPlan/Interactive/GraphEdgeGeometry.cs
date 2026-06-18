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

    // The orthogonal route family an edge draws (rectilinear modes only). Direct is the everyday L
    // (mixed axes, one bend) or Z (matching axes, two bends, corner between the endpoints). Bracket and
    // Saucepan are the clash-avoidance detours the resolver reaches for when an obstacle sits where a
    // Direct route would cross it:
    //   - Bracket ("U"): matching axes, the cross leg slid OUTSIDE the endpoints (above/below or
    //     left/right of the blocking node) - two bends, both ends leaving the same way.
    //   - Saucepan: a "U" bowl that dips around the obstacle, with a short "handle" stub on the source
    //     end, the target end, or BOTH. A handled end leaves on a side perpendicular to the bowl's arms
    //     (so it can keep a horizontal entry/exit while the bowl detours vertically); a direct end
    //     attaches straight onto an arm. So three bends is the minimum (one handle) and four the next
    //     (two handles) - the both-handle form is what the settled MSAGL route shows for an obstacle
    //     squarely between two level nodes.
    internal enum GraphRouteShape
    {
        Direct,
        Bracket,
        Saucepan,
    }

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
    internal readonly record struct GraphRoutePlan(
        GraphConnectionAxis Source,
        GraphConnectionAxis Target,
        GraphRouteShape Shape = GraphRouteShape.Direct,
        double? Primary = null,
        double? Secondary = null,
        bool BowlVertical = false);

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
            GraphConnectionAxis targetAxis,
            double? zCorner = null)
        {
            return routingMode switch
            {
                GraphEdgeRoutingMode.Rectilinear or GraphEdgeRoutingMode.RectilinearToCenter
                    => OrthogonalSegments(start, end, sourceAxis, targetAxis, zCorner),
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

        // The orthogonal (right-angle) routing modes. Only these draw "Z"/"L" paths, so only these get
        // the Z->L promotion and the incoming/outgoing port de-confliction; the spline family keeps its
        // smooth connector untouched by those.
        internal static bool IsRectilinear(GraphEdgeRoutingMode routingMode)
        {
            return routingMode is GraphEdgeRoutingMode.Rectilinear or GraphEdgeRoutingMode.RectilinearToCenter;
        }

        // Reversibility toggle (pending a visual check): true restores the old flip-based
        // incoming/outgoing port de-confliction (GraphPortResolver); false uses the new rules - a
        // horizontal-exit bias (PreferHorizontalExit) plus port offsetting (GraphPortOffsetResolver) so
        // an incoming and outgoing edge may share a side, just separated. A static readonly (not const)
        // so both branches stay reachable and compiled.
        internal static readonly bool UseLegacyRectilinearPorts = false;

        // Rule 2 (new rectilinear ports): bias an outgoing edge's exit toward a horizontal (left/right)
        // side when there is real horizontal room - the far node is at least half a node-width to the
        // side; otherwise keep the resolved axis. Applied to the source end before PromoteZToL.
        internal static GraphConnectionAxis PreferHorizontalExit(GraphConnectionAxis source, double dx, double nodeWidth)
        {
            return dx > nodeWidth / 2.0 ? GraphConnectionAxis.Horizontal : source;
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

        // Promote a "Z" (both endpoints resolved to the same axis) to an "L" once the offset
        // perpendicular to that axis grows past half a node's extent, by flipping the TARGET endpoint
        // to the perpendicular axis - so the edge keeps its source-side run and turns into the target
        // (rather than a Z with an ever-growing middle jog). An already-mixed L (or a straight collapse)
        // is returned unchanged. dx/dy are the absolute centre-to-centre offsets.
        internal static (GraphConnectionAxis Source, GraphConnectionAxis Target) PromoteZToL(
            GraphConnectionAxis sourceAxis,
            GraphConnectionAxis targetAxis,
            double dx,
            double dy,
            double nodeWidth,
            double nodeHeight)
        {
            if (sourceAxis != targetAxis)
            {
                return (sourceAxis, targetAxis);
            }
            if (sourceAxis == GraphConnectionAxis.Horizontal)
            {
                // Horizontal Z: the jog is vertical, so promote once the vertical offset passes the
                // source's half-way line; enter the target vertically (top/bottom).
                if (dy > nodeHeight / 2.0)
                {
                    return (GraphConnectionAxis.Horizontal, GraphConnectionAxis.Vertical);
                }
            }
            else if (dx > nodeWidth / 2.0)
            {
                // Vertical Z: the jog is horizontal; enter the target horizontally (left/right).
                return (GraphConnectionAxis.Vertical, GraphConnectionAxis.Horizontal);
            }
            return (sourceAxis, targetAxis);
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

        // The corner points (a polyline including the endpoints) of an orthogonal route shaped by the
        // per-endpoint axes. Matching axes give a three-point "Z" turning at the midpoint - or at
        // zCorner when supplied, so the middle leg can be slid to dodge a node (the clash resolver);
        // differing axes give a single-corner "L"; endpoints already sharing an axis collapse to a
        // straight [start, end]. Exposed so the clash check tests the exact route the edge draws.
        internal static IReadOnlyList<Point> OrthogonalCorners(
            Point start,
            Point end,
            GraphConnectionAxis sourceAxis,
            GraphConnectionAxis targetAxis,
            double? zCorner = null)
        {
            const double epsilon = 1e-6;
            if (Math.Abs(end.Y - start.Y) < epsilon || Math.Abs(end.X - start.X) < epsilon)
            {
                return [start, end];
            }

            if (sourceAxis == GraphConnectionAxis.Horizontal && targetAxis == GraphConnectionAxis.Horizontal)
            {
                double cornerX = zCorner ?? ((start.X + end.X) / 2.0);
                return [start, new Point(cornerX, start.Y), new Point(cornerX, end.Y), end];
            }

            if (sourceAxis == GraphConnectionAxis.Vertical && targetAxis == GraphConnectionAxis.Vertical)
            {
                double cornerY = zCorner ?? ((start.Y + end.Y) / 2.0);
                return [start, new Point(start.X, cornerY), new Point(end.X, cornerY), end];
            }

            if (sourceAxis == GraphConnectionAxis.Horizontal)
            {
                // Leave horizontally, arrive vertically: corner level with the source, above/below the
                // target.
                return [start, new Point(end.X, start.Y), end];
            }

            // Leave vertically, arrive horizontally: corner above/below the source, level with the
            // target.
            return [start, new Point(start.X, end.Y), end];
        }

        // The corner polyline (endpoints included) of a fully-resolved rectilinear route, from the node
        // centres + size and the route plan. The single source of truth for every orthogonal shape, so
        // the drawn edge (GraphEdgeViewModel) and the clearance check (GraphClashResolver) are built from
        // exactly the same points. Attach points are derived here (not passed in) because the detour
        // shapes pick the node side from where the route runs, not from the other node's direction.
        internal static IReadOnlyList<Point> RouteCorners(
            Point sourceCentre,
            Point targetCentre,
            double width,
            double height,
            GraphRoutePlan plan,
            Point sourceOffset = default,
            Point targetOffset = default)
        {
            double halfWidth = width / 2.0;
            double halfHeight = height / 2.0;
            IReadOnlyList<Point> corners = plan.Shape switch
            {
                GraphRouteShape.Bracket => BracketCorners(sourceCentre, targetCentre, halfWidth, halfHeight, plan.Source, plan.Primary),
                GraphRouteShape.Saucepan => SaucepanCorners(sourceCentre, targetCentre, halfWidth, halfHeight, plan),
                _ => DirectCorners(sourceCentre, targetCentre, width, height, plan),
            };

            // Spread a detour's ports apart: shift the two node-side vertices at each end (the attach
            // point and its first turn) by the port offset, which runs along the side, so the end legs
            // slide while staying orthogonal and the bowl/cross leg is untouched. Direct (L/Z) edges
            // apply their offset elsewhere (in the edge view-model's attach + rebuild path).
            if (plan.Shape != GraphRouteShape.Direct
                && corners.Count >= 4
                && (sourceOffset != default || targetOffset != default))
            {
                var shifted = new List<Point>(corners);
                shifted[0] = Shift(shifted[0], sourceOffset);
                shifted[1] = Shift(shifted[1], sourceOffset);
                shifted[^1] = Shift(shifted[^1], targetOffset);
                shifted[^2] = Shift(shifted[^2], targetOffset);
                return shifted;
            }
            return corners;
        }

        private static Point Shift(Point point, Point delta)
        {
            return new Point(point.X + delta.X, point.Y + delta.Y);
        }

        // A Direct (L/Z) route: attach each end at the centre of the side facing the other node, then
        // turn at the midpoint (or the supplied Z corner). Reproduces the long-standing attach + corner
        // flow exactly, so existing routes are unchanged.
        private static IReadOnlyList<Point> DirectCorners(Point sourceCentre, Point targetCentre, double width, double height, GraphRoutePlan plan)
        {
            Point start = AttachPoint(sourceCentre, width, height, plan.Source, targetCentre);
            Point end = AttachPoint(targetCentre, width, height, plan.Target, sourceCentre);
            return OrthogonalCorners(start, end, plan.Source, plan.Target, plan.Primary);
        }

        // A "U"/bracket: both ends leave along the same axis and the cross leg sits at 'corner', which may
        // be OUTSIDE the span between the nodes so the edge detours around what lies directly between
        // them. The attach side at each end is chosen by which side of the node the corner falls on, so
        // the bracket can rise above (or drop below) two level nodes - something a midpoint Z, whose
        // corner is pinned between the endpoints, cannot do.
        private static IReadOnlyList<Point> BracketCorners(Point a, Point b, double halfWidth, double halfHeight, GraphConnectionAxis axis, double? corner)
        {
            if (axis == GraphConnectionAxis.Vertical)
            {
                double cornerY = corner ?? ((a.Y + b.Y) / 2.0);
                double startY = cornerY <= a.Y ? a.Y - halfHeight : a.Y + halfHeight;
                double endY = cornerY <= b.Y ? b.Y - halfHeight : b.Y + halfHeight;
                return [new Point(a.X, startY), new Point(a.X, cornerY), new Point(b.X, cornerY), new Point(b.X, endY)];
            }

            double cornerX = corner ?? ((a.X + b.X) / 2.0);
            double startX = cornerX <= a.X ? a.X - halfWidth : a.X + halfWidth;
            double endX = cornerX <= b.X ? b.X - halfWidth : b.X + halfWidth;
            return [new Point(startX, a.Y), new Point(cornerX, a.Y), new Point(cornerX, b.Y), new Point(endX, b.Y)];
        }

        // A saucepan: a "U" bowl that dips around an obstacle, with a handle stub on the source end, the
        // target end, or both. A handled end (axis perpendicular to the arms) leaves on a left/right side
        // for a horizontal bowl - so it keeps a horizontal entry/exit while the bowl detours vertically;
        // a direct end attaches straight onto an arm (top/bottom). The two end legs are appended around
        // the bowl's cross leg (which connects the two arm tops), so the list runs source -> target.
        private static IReadOnlyList<Point> SaucepanCorners(Point a, Point b, double halfWidth, double halfHeight, GraphRoutePlan plan)
        {
            var corners = new List<Point>(6);
            if (!plan.BowlVertical)
            {
                // Horizontal bowl (cross leg along X), vertical arms; the dip is in Y. Handled ends leave
                // horizontally (axis H), direct ends attach top/bottom (axis V).
                double bowlY = plan.Primary ?? ((a.Y + b.Y) / 2.0);
                double stub = plan.Secondary ?? halfWidth;
                int sx = b.X >= a.X ? 1 : -1;
                AppendHorizontalBowlEnd(corners, a, halfWidth, halfHeight, plan.Source == GraphConnectionAxis.Horizontal, bowlY, sx, stub, reverse: false);
                AppendHorizontalBowlEnd(corners, b, halfWidth, halfHeight, plan.Target == GraphConnectionAxis.Horizontal, bowlY, -sx, stub, reverse: true);
            }
            else
            {
                // Vertical bowl (cross leg along Y), horizontal arms; the dip is in X. Handled ends leave
                // vertically (axis V), direct ends attach left/right (axis H).
                double bowlX = plan.Primary ?? ((a.X + b.X) / 2.0);
                double stub = plan.Secondary ?? halfHeight;
                int sy = b.Y >= a.Y ? 1 : -1;
                AppendVerticalBowlEnd(corners, a, halfWidth, halfHeight, plan.Source == GraphConnectionAxis.Vertical, bowlX, sy, stub, reverse: false);
                AppendVerticalBowlEnd(corners, b, halfWidth, halfHeight, plan.Target == GraphConnectionAxis.Vertical, bowlX, -sy, stub, reverse: true);
            }
            return corners;
        }

        // One end of a horizontal-bowl saucepan, from the node attach point out to its arm's foot on the
        // bowl line (a handled end adds a horizontal handle stub then a vertical arm; a direct end is a
        // single vertical arm off the top/bottom). 'sx' points along X away from this end; 'reverse'
        // emits the legs bowl-to-node for the target end so the whole list runs source -> target.
        private static void AppendHorizontalBowlEnd(List<Point> corners, Point centre, double halfWidth, double halfHeight, bool handled, double bowlY, int sx, double stub, bool reverse)
        {
            Point[] legs;
            if (handled)
            {
                double startX = centre.X + (sx * halfWidth);
                double armX = startX + (sx * stub);
                legs = [new Point(startX, centre.Y), new Point(armX, centre.Y), new Point(armX, bowlY)];
            }
            else
            {
                double startY = bowlY <= centre.Y ? centre.Y - halfHeight : centre.Y + halfHeight;
                legs = [new Point(centre.X, startY), new Point(centre.X, bowlY)];
            }
            AppendLegs(corners, legs, reverse);
        }

        // The transpose of AppendHorizontalBowlEnd for a vertical-bowl saucepan (horizontal arms,
        // vertical handle stubs); 'sy' points along Y away from this end.
        private static void AppendVerticalBowlEnd(List<Point> corners, Point centre, double halfWidth, double halfHeight, bool handled, double bowlX, int sy, double stub, bool reverse)
        {
            Point[] legs;
            if (handled)
            {
                double startY = centre.Y + (sy * halfHeight);
                double armY = startY + (sy * stub);
                legs = [new Point(centre.X, startY), new Point(centre.X, armY), new Point(bowlX, armY)];
            }
            else
            {
                double startX = bowlX <= centre.X ? centre.X - halfWidth : centre.X + halfWidth;
                legs = [new Point(startX, centre.Y), new Point(bowlX, centre.Y)];
            }
            AppendLegs(corners, legs, reverse);
        }

        private static void AppendLegs(List<Point> corners, Point[] legs, bool reverse)
        {
            if (!reverse)
            {
                corners.AddRange(legs);
                return;
            }
            for (int i = legs.Length - 1; i >= 0; i--)
            {
                corners.Add(legs[i]);
            }
        }

        // The same orthogonal route as contiguous straight segments (each corner-to-corner leg as a
        // bezier on its own chord), built from OrthogonalCorners so the drawn edge and the clash check
        // agree exactly.
        private static IReadOnlyList<GraphEdgeSegment> OrthogonalSegments(
            Point start,
            Point end,
            GraphConnectionAxis sourceAxis,
            GraphConnectionAxis targetAxis,
            double? zCorner)
        {
            return SegmentsFromCorners(OrthogonalCorners(start, end, sourceAxis, targetAxis, zCorner));
        }

        // Chain a corner polyline into contiguous straight bezier segments (each leg on its own chord).
        internal static IReadOnlyList<GraphEdgeSegment> SegmentsFromCorners(IReadOnlyList<Point> corners)
        {
            var segments = new List<GraphEdgeSegment>(Math.Max(1, corners.Count - 1));
            for (int i = 1; i < corners.Count; i++)
            {
                segments.Add(StraightSegment(corners[i - 1], corners[i]));
            }
            if (segments.Count == 0)
            {
                segments.Add(StraightSegment(corners[0], corners[0]));
            }
            return segments;
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
