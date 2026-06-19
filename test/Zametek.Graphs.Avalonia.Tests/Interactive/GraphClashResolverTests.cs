using Avalonia;
using Shouldly;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests
{
    // Tests for edge-vs-node clash avoidance: keep the tentative route if it's clear, else try the
    // other L, else a Z positioned (corner search) to dodge the blocking node(s). Uniform 40x40 nodes,
    // 6px clearance margin. Pure data in/out.
    public class GraphClashResolverTests
    {
        private const int H = (int)GraphConnectionAxis.Horizontal;
        private const int V = (int)GraphConnectionAxis.Vertical;
        private const double c_Node = 40.0;

        private static GraphRoutePlan Resolve(
            IReadOnlyList<PortNode> nodes, int sourceId, int targetId, int tentativeSource, int tentativeTarget)
        {
            PortNode source = nodes.First(n => n.Id == sourceId);
            PortNode target = nodes.First(n => n.Id == targetId);
            return GraphClashResolver.Resolve(
                source, target, (GraphConnectionAxis)tentativeSource, (GraphConnectionAxis)tentativeTarget, nodes, c_Node, c_Node);
        }

        [Fact]
        public void Resolve_NoObstacles_KeepsTentative()
        {
            var nodes = new List<PortNode> { new(1, 0.0, 0.0), new(2, 100.0, 100.0) };

            var route = Resolve(nodes, 1, 2, H, H);

            route.Source.ShouldBe(GraphConnectionAxis.Horizontal);
            route.Target.ShouldBe(GraphConnectionAxis.Horizontal);
            route.Shape.ShouldBe(GraphRouteShape.Direct);
            route.Primary.ShouldBeNull();
        }

        [Fact]
        public void Resolve_TentativeLClashes_SwitchesToTheOtherL()
        {
            // (H,V) leaves node 1 horizontally along y=0; node 3 sits on that leg, while (V,H) is clear.
            var nodes = new List<PortNode> { new(1, 0.0, 0.0), new(2, 100.0, 100.0), new(3, 60.0, 0.0) };

            var route = Resolve(nodes, 1, 2, H, V);

            route.Source.ShouldBe(GraphConnectionAxis.Vertical);
            route.Target.ShouldBe(GraphConnectionAxis.Horizontal);
            route.Shape.ShouldBe(GraphRouteShape.Direct);
            route.Primary.ShouldBeNull();
        }

        [Fact]
        public void Resolve_BothLsClash_FindsAClearHorizontalZCorner()
        {
            // Node 3 blocks the horizontal-exit L's vertical leg (x=100); node 4 blocks the vertical-exit
            // L's horizontal leg (y=100). A horizontal Z still threads a corner between them (~68-72).
            var nodes = new List<PortNode>
            {
                new(1, 0.0, 0.0),
                new(2, 100.0, 100.0),
                new(3, 100.0, 40.0),
                new(4, 40.0, 100.0),
            };

            var route = Resolve(nodes, 1, 2, H, V);

            route.Source.ShouldBe(GraphConnectionAxis.Horizontal);
            route.Target.ShouldBe(GraphConnectionAxis.Horizontal);
            route.Shape.ShouldBe(GraphRouteShape.Direct);
            route.Primary.ShouldNotBeNull();
            route.Primary!.Value.ShouldBeInRange(66.0, 74.0);
        }

        [Fact]
        public void Resolve_ObstacleAcrossMarginBoundary_KeepsThenReroutes()
        {
            // Margin 6px + node half-height 20 -> the y=0 leg clashes when a node centre is within 26 of
            // it. At cy=27 it's clear (tentative L kept); at cy=26 it clashes (reroute to the other L).
            var clear = new List<PortNode> { new(1, 0.0, 0.0), new(2, 100.0, 100.0), new(3, 60.0, 27.0) };
            var kept = Resolve(clear, 1, 2, H, V);
            kept.Source.ShouldBe(GraphConnectionAxis.Horizontal);
            kept.Target.ShouldBe(GraphConnectionAxis.Vertical);

            var blocked = new List<PortNode> { new(1, 0.0, 0.0), new(2, 100.0, 100.0), new(3, 60.0, 26.0) };
            var rerouted = Resolve(blocked, 1, 2, H, V);
            rerouted.Source.ShouldBe(GraphConnectionAxis.Vertical);
            rerouted.Target.ShouldBe(GraphConnectionAxis.Horizontal);
        }

        [Fact]
        public void Resolve_ObstacleDirectlyBetweenLevelNodes_ReturnsAClearBothHandleSaucepan()
        {
            // Source and target on one row with a node squarely between them: every L and in-span Z runs
            // along that row, so none can clear it. The resolver reaches for the both-handle Saucepan
            // (horizontal in and out, dipping around the obstacle) - and it must actually clear the node.
            var nodes = new List<PortNode> { new(1, 0.0, 0.0), new(2, 200.0, 0.0), new(3, 100.0, 0.0) };

            var route = Resolve(nodes, 1, 2, H, V);

            route.Shape.ShouldBe(GraphRouteShape.Saucepan);
            route.Source.ShouldBe(GraphConnectionAxis.Horizontal);
            route.Target.ShouldBe(GraphConnectionAxis.Horizontal);
            RouteClears(nodes[0], nodes[1], route, nodes[2]).ShouldBeTrue();
        }

        // Rebuild the route exactly as it is drawn (RouteCorners) and confirm none of its legs cross the
        // obstacle's rectangle expanded by the clash margin.
        private static bool RouteClears(PortNode source, PortNode target, GraphRoutePlan route, PortNode obstacle)
        {
            const double margin = 6.0;
            double half = (c_Node / 2.0) + margin;
            IReadOnlyList<Point> corners = GraphEdgeGeometry.RouteCorners(
                new Point(source.CentreX, source.CentreY), new Point(target.CentreX, target.CentreY), c_Node, c_Node, route);
            for (int i = 1; i < corners.Count; i++)
            {
                if (SegmentHitsRect(corners[i - 1], corners[i], obstacle.CentreX, obstacle.CentreY, half))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool SegmentHitsRect(Point a, Point b, double cx, double cy, double half)
        {
            double minX = cx - half, maxX = cx + half, minY = cy - half, maxY = cy + half;
            if (Math.Abs(a.Y - b.Y) < 1e-6)
            {
                return a.Y >= minY && a.Y <= maxY && Math.Max(a.X, b.X) >= minX && Math.Min(a.X, b.X) <= maxX;
            }
            return a.X >= minX && a.X <= maxX && Math.Max(a.Y, b.Y) >= minY && Math.Min(a.Y, b.Y) <= maxY;
        }
    }
}
