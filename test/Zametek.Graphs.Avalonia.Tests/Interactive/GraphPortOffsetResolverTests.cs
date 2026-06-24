using Avalonia;
using Shouldly;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests
{
    // Tests for the port-offset resolver: edges that attach to the same node side are spread apart so
    // their ports do not overlap (incoming and outgoing may share a side now). The side is taken from
    // each end's actual attach point; the helper computes those from the axis (the side-centre facing the
    // far node), so a plain L/Z is grouped exactly as before. Pure data in/out.
    public class GraphPortOffsetResolverTests
    {
        private const int H = (int)GraphConnectionAxis.Horizontal;
        private const int V = (int)GraphConnectionAxis.Vertical;
        private const double c_NodeWidth = 60.0;
        private const double c_NodeHeight = 40.0;
        private const double c_Tol = 1e-9;

        private static PortPlacement Edge(IReadOnlyList<PortNode> nodes, int id, int sourceId, int targetId, int sourceAxis, int targetAxis)
        {
            PortNode source = nodes.First(n => n.Id == sourceId);
            PortNode target = nodes.First(n => n.Id == targetId);
            Point sourceAttach = Attach(source, target, (GraphConnectionAxis)sourceAxis);
            Point targetAttach = Attach(target, source, (GraphConnectionAxis)targetAxis);
            return new PortPlacement(id, sourceId, targetId, sourceAttach, targetAttach);
        }

        private static Point Attach(PortNode node, PortNode far, GraphConnectionAxis axis)
        {
            return GraphEdgeGeometry.AttachPoint(
                new Point(node.CentreX, node.CentreY), c_NodeWidth, c_NodeHeight, axis, new Point(far.CentreX, far.CentreY));
        }

        [Fact]
        public void Resolve_SingleEdgeOnEachSide_NoOffset()
        {
            var nodes = new List<PortNode> { new(1, 0.0, 0.0), new(2, 100.0, 0.0) };
            var edges = new List<PortPlacement> { Edge(nodes, 10, 1, 2, H, H) };

            var offsets = GraphPortOffsetResolver.Resolve(nodes, edges, c_NodeWidth, c_NodeHeight);

            offsets[10].SourceOffset.ShouldBe(default);
            offsets[10].TargetOffset.ShouldBe(default);
        }

        [Fact]
        public void Resolve_TwoIncomingOnSameSide_SeparatesThemOrderedByFarPosition()
        {
            // Both enter node 2 from the Left; ordered by the source's Y so the higher one sits higher.
            var nodes = new List<PortNode> { new(1, 0.0, 100.0), new(3, 0.0, 50.0), new(2, 100.0, 100.0) };
            var edges = new List<PortPlacement> { Edge(nodes, 10, 1, 2, H, H), Edge(nodes, 11, 3, 2, H, H) };

            var offsets = GraphPortOffsetResolver.Resolve(nodes, edges, c_NodeWidth, c_NodeHeight);

            // Gap 7, centred -> +/-3.5 on Y. Edge 11 (source higher up, Y=50) takes the upper slot.
            offsets[11].TargetOffset.X.ShouldBe(0.0, c_Tol);
            offsets[11].TargetOffset.Y.ShouldBe(-3.5, c_Tol);
            offsets[10].TargetOffset.Y.ShouldBe(3.5, c_Tol);
            // Their source ends are each alone on their own node side.
            offsets[10].SourceOffset.ShouldBe(default);
            offsets[11].SourceOffset.ShouldBe(default);
        }

        [Fact]
        public void Resolve_IncomingAndOutgoingShareASide_AreSeparatedNotRerouted()
        {
            // Node 2 has an incoming (10) and an outgoing (11) both on its Left side: allowed now, just
            // offset apart. The axes are NOT changed (no flipping).
            var nodes = new List<PortNode> { new(1, 0.0, 100.0), new(2, 100.0, 100.0), new(3, 0.0, 50.0) };
            var edges = new List<PortPlacement> { Edge(nodes, 10, 1, 2, H, H), Edge(nodes, 11, 2, 3, H, H) };

            var offsets = GraphPortOffsetResolver.Resolve(nodes, edges, c_NodeWidth, c_NodeHeight);

            // Outgoing 11 faces node 3 (Y=50, higher) -> upper slot; incoming 10 faces node 1 (Y=100).
            offsets[11].SourceOffset.Y.ShouldBe(-3.5, c_Tol);
            offsets[10].TargetOffset.Y.ShouldBe(3.5, c_Tol);
            // Each one's other end is alone on node 1 / node 3.
            offsets[10].SourceOffset.ShouldBe(default);
            offsets[11].TargetOffset.ShouldBe(default);
        }

        [Fact]
        public void Resolve_CrowdedSide_ClampsTheGapToTheSideLength()
        {
            // Five edges into node 99 from the left; node height 40 -> span 32 -> gap 32/4 = 8 ... but
            // capped at the 7px target, so the gap stays 7. Use a SHORT side to force the clamp instead.
            var nodes = new List<PortNode>
            {
                new(99, 100.0, 100.0),
                new(1, 0.0, 10.0),
                new(2, 0.0, 20.0),
                new(3, 0.0, 30.0),
                new(4, 0.0, 40.0),
                new(5, 0.0, 50.0),
            };
            var edges = new List<PortPlacement>
            {
                Edge(nodes, 10, 1, 99, H, H),
                Edge(nodes, 11, 2, 99, H, H),
                Edge(nodes, 12, 3, 99, H, H),
                Edge(nodes, 13, 4, 99, H, H),
                Edge(nodes, 14, 5, 99, H, H),
            };

            // Short side (height 20): span = 20 - 8 = 12; gap = 12 / (5 - 1) = 3 (< 7 target).
            var offsets = GraphPortOffsetResolver.Resolve(nodes, edges, c_NodeWidth, 20.0);

            // Sources are ordered by Y (10..50); centred offsets are -6,-3,0,3,6 with a 3px gap.
            offsets[10].TargetOffset.Y.ShouldBe(-6.0, c_Tol);
            offsets[11].TargetOffset.Y.ShouldBe(-3.0, c_Tol);
            offsets[12].TargetOffset.Y.ShouldBe(0.0, c_Tol);
            offsets[13].TargetOffset.Y.ShouldBe(3.0, c_Tol);
            offsets[14].TargetOffset.Y.ShouldBe(6.0, c_Tol);
        }

        [Fact]
        public void Resolve_AttachOnSideFacingAwayFromFarNode_GroupsByActualSide()
        {
            // Both edges leave node 1 on its LEFT side though their targets are to the right - as a
            // Bracket/Saucepan detour does. Grouping must follow the real attach point (Left), not the
            // targets' direction (which would wrongly say Right), so the two are spread apart.
            var nodes = new List<PortNode> { new(1, 0.0, 0.0), new(2, 100.0, 50.0), new(3, 100.0, -50.0) };
            double leftX = -c_NodeWidth / 2.0;
            var edges = new List<PortPlacement>
            {
                new(10, 1, 2, new Point(leftX, 0.0), new Point(70.0, 50.0)),
                new(11, 1, 3, new Point(leftX, 0.0), new Point(70.0, -50.0)),
            };

            var offsets = GraphPortOffsetResolver.Resolve(nodes, edges, c_NodeWidth, c_NodeHeight);

            // Ordered by the far node's Y: node 3 (-50) is higher -> upper slot; spread +/-3.5 on Y.
            offsets[11].SourceOffset.Y.ShouldBe(-3.5, c_Tol);
            offsets[10].SourceOffset.Y.ShouldBe(3.5, c_Tol);
        }
    }
}
