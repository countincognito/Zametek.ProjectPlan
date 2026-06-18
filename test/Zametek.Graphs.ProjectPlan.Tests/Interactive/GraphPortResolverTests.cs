using Shouldly;
using Xunit;

namespace Zametek.Graphs.ProjectPlan.Tests
{
    // Tests for the incoming/outgoing port de-confliction: at any node, a side may carry two incoming
    // or two outgoing edges, but never a mix of an arrival and a departure. Pure data in/out - no
    // Avalonia or view-model required.
    public class GraphPortResolverTests
    {
        private const int H = (int)GraphConnectionAxis.Horizontal;
        private const int V = (int)GraphConnectionAxis.Vertical;

        private enum Side { Left, Right, Top, Bottom }

        private static Side SideAt(PortNode node, PortNode far, GraphConnectionAxis nearAxis)
        {
            if (nearAxis == GraphConnectionAxis.Horizontal)
            {
                return far.CentreX >= node.CentreX ? Side.Right : Side.Left;
            }
            return far.CentreY >= node.CentreY ? Side.Bottom : Side.Top;
        }

        // The core invariant: no node side ends up with both an incoming and an outgoing edge.
        private static void ShouldHaveNoMixedSides(
            IReadOnlyList<PortNode> nodes,
            IReadOnlyList<PortEdge> edges,
            IReadOnlyDictionary<int, (GraphConnectionAxis Source, GraphConnectionAxis Target)> resolved)
        {
            Dictionary<int, PortNode> byId = nodes.ToDictionary(n => n.Id);
            var dir = new Dictionary<(int Node, Side Side), (bool In, bool Out)>();
            foreach (PortEdge e in edges)
            {
                if (e.SourceId == e.TargetId)
                {
                    continue;
                }
                (GraphConnectionAxis source, GraphConnectionAxis target) = resolved[e.Id];
                Mark(dir, (e.SourceId, SideAt(byId[e.SourceId], byId[e.TargetId], source)), outgoing: true);
                Mark(dir, (e.TargetId, SideAt(byId[e.TargetId], byId[e.SourceId], target)), outgoing: false);
            }
            foreach (KeyValuePair<(int Node, Side Side), (bool In, bool Out)> entry in dir)
            {
                (entry.Value.In && entry.Value.Out)
                    .ShouldBeFalse($"node {entry.Key.Node} side {entry.Key.Side} has both incoming and outgoing");
            }
        }

        private static void Mark(
            Dictionary<(int Node, Side Side), (bool In, bool Out)> dir,
            (int Node, Side Side) key,
            bool outgoing)
        {
            dir.TryGetValue(key, out (bool In, bool Out) value);
            dir[key] = outgoing ? (value.In, true) : (true, value.Out);
        }

        private static PortEdge Edge(int id, int sourceId, int targetId, int sourceAxis, int targetAxis)
        {
            return new PortEdge(id, sourceId, targetId, (GraphConnectionAxis)sourceAxis, (GraphConnectionAxis)targetAxis);
        }

        [Fact]
        public void Resolve_TwoIncomingOnSameSide_IsLeftUnchanged()
        {
            // node 2 receives both edges from the left -> two incoming on the Left side is allowed.
            var nodes = new List<PortNode> { new(1, 0.0, 100.0), new(3, 0.0, 50.0), new(2, 100.0, 100.0) };
            var edges = new List<PortEdge> { Edge(10, 1, 2, H, H), Edge(11, 3, 2, H, H) };

            var resolved = GraphPortResolver.Resolve(nodes, edges);

            resolved[10].ShouldBe((GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal));
            resolved[11].ShouldBe((GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal));
            ShouldHaveNoMixedSides(nodes, edges, resolved);
        }

        [Fact]
        public void Resolve_IncomingAndOutgoingBothL_FlipsTheIncoming()
        {
            // Pass-through node 2: incoming 10 (L) and outgoing 11 (L) both land on Top -> incoming yields.
            var nodes = new List<PortNode> { new(1, 0.0, 0.0), new(2, 100.0, 100.0), new(3, 150.0, 0.0) };
            var edges = new List<PortEdge> { Edge(10, 1, 2, H, V), Edge(11, 2, 3, V, H) };

            var resolved = GraphPortResolver.Resolve(nodes, edges);

            // Incoming 10 rotated H,V -> V,H (now enters node 2 from the left); outgoing 11 kept.
            resolved[10].ShouldBe((GraphConnectionAxis.Vertical, GraphConnectionAxis.Horizontal));
            resolved[11].ShouldBe((GraphConnectionAxis.Vertical, GraphConnectionAxis.Horizontal));
            ShouldHaveNoMixedSides(nodes, edges, resolved);
        }

        [Fact]
        public void Resolve_OneZandOneL_KeepsTheZandFlipsTheL()
        {
            // At node 2: incoming 10 (L) and outgoing 11 (Z) both land on Right -> keep the Z, flip the L.
            var nodes = new List<PortNode> { new(1, 300.0, 0.0), new(2, 100.0, 100.0), new(3, 200.0, 100.0) };
            var edges = new List<PortEdge> { Edge(10, 1, 2, V, H), Edge(11, 2, 3, H, H) };

            var resolved = GraphPortResolver.Resolve(nodes, edges);

            resolved[10].ShouldBe((GraphConnectionAxis.Horizontal, GraphConnectionAxis.Vertical));
            resolved[11].ShouldBe((GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal));
            ShouldHaveNoMixedSides(nodes, edges, resolved);
        }

        [Fact]
        public void Resolve_NoConflict_LeavesAxesUnchanged()
        {
            // A simple in-line chain: 1 -> 2 -> 3, all horizontal. No side mixes in and out.
            var nodes = new List<PortNode> { new(1, 0.0, 0.0), new(2, 100.0, 0.0), new(3, 200.0, 0.0) };
            var edges = new List<PortEdge> { Edge(10, 1, 2, H, H), Edge(11, 2, 3, H, H) };

            var resolved = GraphPortResolver.Resolve(nodes, edges);

            resolved[10].ShouldBe((GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal));
            resolved[11].ShouldBe((GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal));
            ShouldHaveNoMixedSides(nodes, edges, resolved);
        }

        [Fact]
        public void Resolve_CascadingConflict_IteratesToConvergence()
        {
            // Flipping the incoming edge at node 2 pushes its source end onto node 1's Bottom, where it
            // collides with another incoming edge - which the next iteration must then resolve too.
            var nodes = new List<PortNode>
            {
                new(1, 0.0, 50.0),
                new(2, 100.0, 100.0),
                new(3, 200.0, 50.0),
                new(4, 0.0, 200.0),
            };
            var edges = new List<PortEdge>
            {
                Edge(10, 1, 2, H, V),   // 1 -> 2, conflicts at node 2's Top with edge 11
                Edge(11, 2, 3, V, H),   // 2 -> 3, kept on node 2's Top
                Edge(12, 4, 1, H, V),   // 4 -> 1, collides at node 1 once edge 10 is flipped
            };

            var resolved = GraphPortResolver.Resolve(nodes, edges);

            // The whole arrangement must be conflict-free after convergence.
            ShouldHaveNoMixedSides(nodes, edges, resolved);
        }
    }
}
