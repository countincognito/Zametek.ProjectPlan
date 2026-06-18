using Avalonia;

namespace Zametek.Graphs.ProjectPlan
{
    // Steers a single rectilinear edge's route around the nodes it is not connected to (edge-vs-node
    // clash avoidance), appended to the end of the rectilinear-port cascade. If the route the earlier
    // rules chose is clear it is kept; otherwise candidates are tried in preference order and the first
    // clear one wins:
    //   1. the next preferable L route - the horizontal-exit L, then the vertical-exit L;
    //   2. an in-span Z route - sliding the middle leg to a corner between the endpoints;
    //   3. a Saucepan ("a Z with an extra bend") - a handle stub then a bowl that dips around the
    //      blocking node and turns into the other end on a perpendicular side;
    //   4. a Bracket ("U") - both ends leaving the same way with the cross leg slid OUTSIDE the
    //      endpoints, over/under (or left/right of) the blocking node.
    // The order matches the agreed rule set: L, then Z, then Saucepan, then U. The detour searches
    // (3, 4) try positions just clear of each node and reach as far as needed across the arrangement.
    // If nothing is clear the tentative route is kept (the drop-time MSAGL reroute resolves it). Each
    // edge is judged independently against the node rectangles, so no global iteration is needed; node
    // sizes are taken as uniform. Every candidate is measured through GraphEdgeGeometry.RouteCorners -
    // the exact same builder the edge draws with - so "clear" means clear of what is actually rendered.
    internal static class GraphClashResolver
    {
        private const double c_Margin = 6.0;
        private const int c_CornerSamples = 21;
        // Place a detour just past a node's border (half the margin) so it clears without hugging.
        private const double c_EdgeClearance = c_Margin / 2.0;

        public static GraphRoutePlan Resolve(
            PortNode source,
            PortNode target,
            GraphConnectionAxis tentativeSource,
            GraphConnectionAxis tentativeTarget,
            IReadOnlyList<PortNode> nodes,
            double nodeWidth,
            double nodeHeight)
        {
            var a = new Point(source.CentreX, source.CentreY);
            var b = new Point(target.CentreX, target.CentreY);
            int sId = source.Id;
            int tId = target.Id;

            // Keep the route the earlier rules chose if it is already clear (a Z uses its midpoint).
            var tentative = new GraphRoutePlan(tentativeSource, tentativeTarget);
            if (IsClear(tentative, a, b, nodeWidth, nodeHeight, nodes, sId, tId))
            {
                return tentative;
            }

            // Rule 1: L routes first, the horizontal-exit one preferred.
            var horizontalL = new GraphRoutePlan(GraphConnectionAxis.Horizontal, GraphConnectionAxis.Vertical);
            if (IsClear(horizontalL, a, b, nodeWidth, nodeHeight, nodes, sId, tId))
            {
                return horizontalL;
            }
            var verticalL = new GraphRoutePlan(GraphConnectionAxis.Vertical, GraphConnectionAxis.Horizontal);
            if (IsClear(verticalL, a, b, nodeWidth, nodeHeight, nodes, sId, tId))
            {
                return verticalL;
            }

            // Rule 2: in-span Z routes (corner between the endpoints), horizontal preferred.
            double? horizontalZ = SearchInSpanZ(a, b, nodeWidth, nodeHeight, GraphConnectionAxis.Horizontal, nodes, sId, tId);
            if (horizontalZ is not null)
            {
                return new GraphRoutePlan(GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal, GraphRouteShape.Direct, horizontalZ);
            }
            double? verticalZ = SearchInSpanZ(a, b, nodeWidth, nodeHeight, GraphConnectionAxis.Vertical, nodes, sId, tId);
            if (verticalZ is not null)
            {
                return new GraphRoutePlan(GraphConnectionAxis.Vertical, GraphConnectionAxis.Vertical, GraphRouteShape.Direct, verticalZ);
            }

            // Rule 3: a Saucepan (a Z with an extra bend) before the bracket.
            GraphRoutePlan? saucepan = SearchSaucepan(a, b, nodeWidth, nodeHeight, nodes, sId, tId);
            if (saucepan is not null)
            {
                return saucepan.Value;
            }

            // Rule 4: a Bracket ("U") with the cross leg slid outside the endpoints.
            GraphRoutePlan? bracket = SearchBracket(a, b, nodeWidth, nodeHeight, nodes, sId, tId);
            if (bracket is not null)
            {
                return bracket.Value;
            }

            // Nothing clear: keep the tentative route.
            return tentative;
        }

        private static bool IsClear(
            GraphRoutePlan plan,
            Point a,
            Point b,
            double nodeWidth,
            double nodeHeight,
            IReadOnlyList<PortNode> nodes,
            int sourceId,
            int targetId)
        {
            IReadOnlyList<Point> corners = GraphEdgeGeometry.RouteCorners(a, b, nodeWidth, nodeHeight, plan);
            return IsPolylineClear(corners, nodes, sourceId, targetId, nodeWidth, nodeHeight);
        }

        // Slide an in-span Z's middle leg to a corner BETWEEN the endpoints that clears every blocking
        // node, nearest the midpoint first. Returns null if none of the sampled positions is clear (or
        // the endpoints align, so the Z collapses to a straight line).
        private static double? SearchInSpanZ(
            Point a,
            Point b,
            double nodeWidth,
            double nodeHeight,
            GraphConnectionAxis axis,
            IReadOnlyList<PortNode> nodes,
            int sourceId,
            int targetId)
        {
            Point start = GraphEdgeGeometry.AttachPoint(a, nodeWidth, nodeHeight, axis, b);
            Point end = GraphEdgeGeometry.AttachPoint(b, nodeWidth, nodeHeight, axis, a);
            bool horizontal = axis == GraphConnectionAxis.Horizontal;
            double lo = horizontal ? Math.Min(start.X, end.X) : Math.Min(start.Y, end.Y);
            double hi = horizontal ? Math.Max(start.X, end.X) : Math.Max(start.Y, end.Y);
            if (hi - lo < 1e-6)
            {
                return null;
            }

            double mid = (lo + hi) / 2.0;
            var samples = new List<double>(c_CornerSamples);
            for (int i = 0; i < c_CornerSamples; i++)
            {
                samples.Add(lo + ((hi - lo) * i / (c_CornerSamples - 1)));
            }
            samples.Sort((x, y) => Math.Abs(x - mid).CompareTo(Math.Abs(y - mid)));

            foreach (double corner in samples)
            {
                var plan = new GraphRoutePlan(axis, axis, GraphRouteShape.Direct, corner);
                if (IsClear(plan, a, b, nodeWidth, nodeHeight, nodes, sourceId, targetId))
                {
                    return corner;
                }
            }
            return null;
        }

        // A Saucepan: try each orientation (handle at the source or the target, leaving horizontally or
        // vertically) and, within it, slide the bowl's cross leg to a position just clear of a node,
        // nearest the centre line first. The horizontal-exit orientations are tried first to keep the
        // agreed horizontal-exit bias. Returns the first clear plan, or null.
        private static GraphRoutePlan? SearchSaucepan(
            Point a,
            Point b,
            double nodeWidth,
            double nodeHeight,
            IReadOnlyList<PortNode> nodes,
            int sourceId,
            int targetId)
        {
            // (handleAtSource, handle axis); horizontal handle first.
            (bool HandleAtSource, GraphConnectionAxis HandleAxis)[] orientations =
            [
                (true, GraphConnectionAxis.Horizontal),
                (false, GraphConnectionAxis.Horizontal),
                (true, GraphConnectionAxis.Vertical),
                (false, GraphConnectionAxis.Vertical),
            ];

            foreach ((bool handleAtSource, GraphConnectionAxis handleAxis) in orientations)
            {
                bool bowlIsY = handleAxis == GraphConnectionAxis.Horizontal;
                double ideal = bowlIsY ? (a.Y + b.Y) / 2.0 : (a.X + b.X) / 2.0;
                IReadOnlyList<double> bowls = EdgeCandidates(nodes, bowlIsY, nodeWidth, nodeHeight, ideal);
                GraphConnectionAxis perpendicular = bowlIsY ? GraphConnectionAxis.Vertical : GraphConnectionAxis.Horizontal;

                foreach (double bowl in bowls)
                {
                    GraphRoutePlan plan = handleAtSource
                        ? new GraphRoutePlan(handleAxis, perpendicular, GraphRouteShape.Saucepan, bowl, null, true)
                        : new GraphRoutePlan(perpendicular, handleAxis, GraphRouteShape.Saucepan, bowl, null, false);
                    if (IsClear(plan, a, b, nodeWidth, nodeHeight, nodes, sourceId, targetId))
                    {
                        return plan;
                    }
                }
            }
            return null;
        }

        // A Bracket ("U"): slide the cross leg to a position OUTSIDE the endpoints (over/under for a
        // vertical-sided bracket, left/right for a horizontal-sided one) that clears every blocking node,
        // nearest the centre line first. Vertical sides (jump above/below) are tried first. Returns the
        // first clear plan, or null.
        private static GraphRoutePlan? SearchBracket(
            Point a,
            Point b,
            double nodeWidth,
            double nodeHeight,
            IReadOnlyList<PortNode> nodes,
            int sourceId,
            int targetId)
        {
            GraphConnectionAxis[] axes = [GraphConnectionAxis.Vertical, GraphConnectionAxis.Horizontal];
            foreach (GraphConnectionAxis axis in axes)
            {
                bool crossIsY = axis == GraphConnectionAxis.Vertical;
                double ideal = crossIsY ? (a.Y + b.Y) / 2.0 : (a.X + b.X) / 2.0;
                IReadOnlyList<double> corners = EdgeCandidates(nodes, crossIsY, nodeWidth, nodeHeight, ideal);
                foreach (double corner in corners)
                {
                    var plan = new GraphRoutePlan(axis, axis, GraphRouteShape.Bracket, corner);
                    if (IsClear(plan, a, b, nodeWidth, nodeHeight, nodes, sourceId, targetId))
                    {
                        return plan;
                    }
                }
            }
            return null;
        }

        // Detour positions taken just clear of each node's border (top/bottom edges when the detour leg
        // runs along Y, left/right edges when along X), sorted nearest the ideal (centre-line) first - so
        // a detour clings as closely to the straight route as the obstacles allow.
        private static IReadOnlyList<double> EdgeCandidates(
            IReadOnlyList<PortNode> nodes,
            bool alongY,
            double nodeWidth,
            double nodeHeight,
            double ideal)
        {
            double half = (alongY ? nodeHeight / 2.0 : nodeWidth / 2.0) + c_Margin + c_EdgeClearance;
            var candidates = new List<double>((nodes.Count * 2) + 1) { ideal };
            foreach (PortNode node in nodes)
            {
                double centre = alongY ? node.CentreY : node.CentreX;
                candidates.Add(centre - half);
                candidates.Add(centre + half);
            }
            candidates.Sort((x, y) => Math.Abs(x - ideal).CompareTo(Math.Abs(y - ideal)));
            return candidates;
        }

        private static bool IsPolylineClear(
            IReadOnlyList<Point> corners,
            IReadOnlyList<PortNode> nodes,
            int sourceId,
            int targetId,
            double nodeWidth,
            double nodeHeight)
        {
            double halfWidth = (nodeWidth / 2.0) + c_Margin;
            double halfHeight = (nodeHeight / 2.0) + c_Margin;
            for (int i = 1; i < corners.Count; i++)
            {
                Point a = corners[i - 1];
                Point b = corners[i];
                foreach (PortNode node in nodes)
                {
                    if (node.Id == sourceId || node.Id == targetId)
                    {
                        continue;
                    }
                    if (SegmentIntersectsRect(a, b, node.CentreX, node.CentreY, halfWidth, halfHeight))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // Axis-aligned segment vs an axis-aligned rectangle (centre + half extents). All rectilinear
        // legs are axis-aligned, so the segment is classified as horizontal or vertical.
        private static bool SegmentIntersectsRect(Point a, Point b, double centreX, double centreY, double halfWidth, double halfHeight)
        {
            double minX = centreX - halfWidth;
            double maxX = centreX + halfWidth;
            double minY = centreY - halfHeight;
            double maxY = centreY + halfHeight;

            if (Math.Abs(a.Y - b.Y) < 1e-6)
            {
                // Horizontal segment at y, spanning [a.X, b.X].
                if (a.Y < minY || a.Y > maxY)
                {
                    return false;
                }
                return Math.Max(a.X, b.X) >= minX && Math.Min(a.X, b.X) <= maxX;
            }

            // Vertical segment at x, spanning [a.Y, b.Y].
            if (a.X < minX || a.X > maxX)
            {
                return false;
            }
            return Math.Max(a.Y, b.Y) >= minY && Math.Min(a.Y, b.Y) <= maxY;
        }
    }
}
