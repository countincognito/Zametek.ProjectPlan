using Shouldly;
using Xunit;

namespace Zametek.Graphs.ProjectPlan.Tests
{
    // Tests for edge-vs-node clash avoidance: keep the tentative route if it's clear, else try the
    // other L, else a Z positioned (corner search) to dodge the blocking node(s). Uniform 40x40 nodes,
    // 6px clearance margin. Pure data in/out.
    public class GraphClashResolverTests
    {
        private const int H = (int)GraphConnectionAxis.Horizontal;
        private const int V = (int)GraphConnectionAxis.Vertical;
        private const double c_Node = 40.0;

        private static (GraphConnectionAxis Source, GraphConnectionAxis Target, double? ZCorner) Resolve(
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
            route.ZCorner.ShouldBeNull();
        }

        [Fact]
        public void Resolve_TentativeLClashes_SwitchesToTheOtherL()
        {
            // (H,V) leaves node 1 horizontally along y=0; node 3 sits on that leg, while (V,H) is clear.
            var nodes = new List<PortNode> { new(1, 0.0, 0.0), new(2, 100.0, 100.0), new(3, 60.0, 0.0) };

            var route = Resolve(nodes, 1, 2, H, V);

            route.Source.ShouldBe(GraphConnectionAxis.Vertical);
            route.Target.ShouldBe(GraphConnectionAxis.Horizontal);
            route.ZCorner.ShouldBeNull();
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
            route.ZCorner.ShouldNotBeNull();
            route.ZCorner!.Value.ShouldBeInRange(66.0, 74.0);
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
    }
}
