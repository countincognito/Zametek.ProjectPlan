using Avalonia;

namespace Zametek.Graphs.ProjectPlan
{
    // Steers a single rectilinear edge's route around the nodes it is not connected to (edge-vs-node
    // clash avoidance), appended to the end of the rectilinear-port cascade. If the route the earlier
    // rules chose is clear it is kept; otherwise candidates are tried in preference order and the first
    // clear one wins:
    //   1. the next preferable L route - the horizontal-exit L, then the vertical-exit L (Rule 1);
    //   2. a Z route positioned to avoid the clash - horizontal then vertical, sliding the middle leg
    //      to a corner that clears the blocking node(s) (Rule 2).
    // L routes are always tried before Z routes (even when the route that clashed was itself a Z). If
    // nothing is clear the tentative route is kept (the drop-time MSAGL reroute resolves it). Each edge
    // is judged independently against the node rectangles, so no global iteration is needed; node sizes
    // are taken as uniform. Clearance is a small margin so edges do not hug a node's border.
    internal static class GraphClashResolver
    {
        private const double c_Margin = 6.0;
        private const int c_CornerSamples = 17;

        public static (GraphConnectionAxis Source, GraphConnectionAxis Target, double? ZCorner) Resolve(
            PortNode source,
            PortNode target,
            GraphConnectionAxis tentativeSource,
            GraphConnectionAxis tentativeTarget,
            IReadOnlyList<PortNode> nodes,
            double nodeWidth,
            double nodeHeight)
        {
            // Keep the route the earlier rules chose if it is already clear (a Z uses its midpoint).
            if (IsCandidateClear(source, target, tentativeSource, tentativeTarget, zCorner: null, nodes, nodeWidth, nodeHeight))
            {
                return (tentativeSource, tentativeTarget, null);
            }

            // Rule 1: L routes first, the horizontal-exit one preferred.
            if (IsCandidateClear(source, target, GraphConnectionAxis.Horizontal, GraphConnectionAxis.Vertical, null, nodes, nodeWidth, nodeHeight))
            {
                return (GraphConnectionAxis.Horizontal, GraphConnectionAxis.Vertical, null);
            }
            if (IsCandidateClear(source, target, GraphConnectionAxis.Vertical, GraphConnectionAxis.Horizontal, null, nodes, nodeWidth, nodeHeight))
            {
                return (GraphConnectionAxis.Vertical, GraphConnectionAxis.Horizontal, null);
            }

            // Rule 2: Z routes, positioned to clear the blocking node(s); horizontal preferred.
            double? horizontalCorner = SearchZCorner(source, target, GraphConnectionAxis.Horizontal, nodes, nodeWidth, nodeHeight);
            if (horizontalCorner is not null)
            {
                return (GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal, horizontalCorner);
            }
            double? verticalCorner = SearchZCorner(source, target, GraphConnectionAxis.Vertical, nodes, nodeWidth, nodeHeight);
            if (verticalCorner is not null)
            {
                return (GraphConnectionAxis.Vertical, GraphConnectionAxis.Vertical, verticalCorner);
            }

            // Nothing clear: keep the tentative route.
            return (tentativeSource, tentativeTarget, null);
        }

        private static bool IsCandidateClear(
            PortNode source,
            PortNode target,
            GraphConnectionAxis sourceAxis,
            GraphConnectionAxis targetAxis,
            double? zCorner,
            IReadOnlyList<PortNode> nodes,
            double nodeWidth,
            double nodeHeight)
        {
            Point start = AttachPoint(source, sourceAxis, target, nodeWidth, nodeHeight);
            Point end = AttachPoint(target, targetAxis, source, nodeWidth, nodeHeight);
            IReadOnlyList<Point> corners = GraphEdgeGeometry.OrthogonalCorners(start, end, sourceAxis, targetAxis, zCorner);
            return IsPolylineClear(corners, nodes, source.Id, target.Id, nodeWidth, nodeHeight);
        }

        // Slide a Z route's middle leg to a corner (along the route's primary axis) that clears every
        // blocking node, trying positions nearest the midpoint first. Returns null if none of the
        // sampled positions is clear (or the endpoints align, so the Z collapses to a straight line).
        private static double? SearchZCorner(
            PortNode source,
            PortNode target,
            GraphConnectionAxis zAxis,
            IReadOnlyList<PortNode> nodes,
            double nodeWidth,
            double nodeHeight)
        {
            Point start = AttachPoint(source, zAxis, target, nodeWidth, nodeHeight);
            Point end = AttachPoint(target, zAxis, source, nodeWidth, nodeHeight);
            bool horizontalZ = zAxis == GraphConnectionAxis.Horizontal;
            double lo = horizontalZ ? Math.Min(start.X, end.X) : Math.Min(start.Y, end.Y);
            double hi = horizontalZ ? Math.Max(start.X, end.X) : Math.Max(start.Y, end.Y);
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
            samples.Sort((a, b) => Math.Abs(a - mid).CompareTo(Math.Abs(b - mid)));

            foreach (double corner in samples)
            {
                IReadOnlyList<Point> corners = GraphEdgeGeometry.OrthogonalCorners(start, end, zAxis, zAxis, corner);
                if (IsPolylineClear(corners, nodes, source.Id, target.Id, nodeWidth, nodeHeight))
                {
                    return corner;
                }
            }
            return null;
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

        private static Point AttachPoint(PortNode node, GraphConnectionAxis axis, PortNode toward, double nodeWidth, double nodeHeight)
        {
            return GraphEdgeGeometry.AttachPoint(
                new Point(node.CentreX, node.CentreY),
                nodeWidth,
                nodeHeight,
                axis,
                new Point(toward.CentreX, toward.CentreY));
        }
    }
}
