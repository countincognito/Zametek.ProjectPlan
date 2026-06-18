using Avalonia;

namespace Zametek.Graphs.ProjectPlan
{
    // Spreads the edges that attach to the same node side apart so their ports (and arrowheads) do not
    // overlap - the replacement for the flip-based de-confliction. An incoming and an outgoing edge may
    // now share a side; every edge attaching to a side is simply offset along it. Used only by the
    // rectilinear families and only for the drag-time approximation. Returns a per-edge perpendicular
    // attach-point offset for each end (zero when an end is the only edge on its side).
    //
    // Ports on a side are ordered by the far endpoint's position on the perpendicular axis (so edges
    // fan out toward their destinations without crossing), then spread by a fixed gap centred on the
    // side - clamped to the side length so they stay on the node border. Node sizes are taken as
    // uniform (the arrow/vertex graphs use one node size), so a single width/height is passed in.
    internal static class GraphPortOffsetResolver
    {
        // Target separation between adjacent ports, and the keep-off-the-corner margin, in pixels.
        private const double c_PortGap = 7.0;
        private const double c_PortMargin = 4.0;

        private enum Side
        {
            Left,
            Right,
            Top,
            Bottom,
        }

        private readonly record struct Port(int EdgeId, bool IsSource, double OrderKey);

        public static IReadOnlyDictionary<int, (Point SourceOffset, Point TargetOffset)> Resolve(
            IReadOnlyList<PortNode> nodes,
            IReadOnlyList<PortEdge> edges,
            double nodeWidth,
            double nodeHeight)
        {
            var centreById = new Dictionary<int, PortNode>(nodes.Count);
            foreach (PortNode node in nodes)
            {
                centreById[node.Id] = node;
            }

            var result = new Dictionary<int, (Point SourceOffset, Point TargetOffset)>(edges.Count);
            var groups = new Dictionary<(int Node, Side Side), List<Port>>();
            foreach (PortEdge edge in edges)
            {
                result[edge.Id] = (default, default);
                if (edge.SourceId == edge.TargetId
                    || !centreById.TryGetValue(edge.SourceId, out PortNode sourceNode)
                    || !centreById.TryGetValue(edge.TargetId, out PortNode targetNode))
                {
                    continue;
                }
                Side sourceSide = SideAt(sourceNode, targetNode, edge.SourceAxis);
                Add(groups, (edge.SourceId, sourceSide), new Port(edge.Id, IsSource: true, OrderKey(sourceSide, targetNode)));
                Side targetSide = SideAt(targetNode, sourceNode, edge.TargetAxis);
                Add(groups, (edge.TargetId, targetSide), new Port(edge.Id, IsSource: false, OrderKey(targetSide, sourceNode)));
            }

            foreach (KeyValuePair<(int Node, Side Side), List<Port>> group in groups)
            {
                List<Port> ports = group.Value;
                if (ports.Count <= 1)
                {
                    continue;
                }
                ports.Sort((a, b) => a.OrderKey.CompareTo(b.OrderKey));

                bool horizontalSide = group.Key.Side is Side.Left or Side.Right;
                double span = (horizontalSide ? nodeHeight : nodeWidth) - (2.0 * c_PortMargin);
                double gap = Math.Min(c_PortGap, span > 0.0 ? span / (ports.Count - 1) : 0.0);

                for (int i = 0; i < ports.Count; i++)
                {
                    double along = (i - ((ports.Count - 1) / 2.0)) * gap;
                    Point delta = horizontalSide ? new Point(0.0, along) : new Point(along, 0.0);
                    Port port = ports[i];
                    (Point SourceOffset, Point TargetOffset) current = result[port.EdgeId];
                    result[port.EdgeId] = port.IsSource
                        ? (delta, current.TargetOffset)
                        : (current.SourceOffset, delta);
                }
            }
            return result;
        }

        private static Side SideAt(PortNode node, PortNode far, GraphConnectionAxis nearAxis)
        {
            if (nearAxis == GraphConnectionAxis.Horizontal)
            {
                return far.CentreX >= node.CentreX ? Side.Right : Side.Left;
            }
            return far.CentreY >= node.CentreY ? Side.Bottom : Side.Top;
        }

        // Distribute along a side by the far endpoint's position on the perpendicular axis: for a
        // left/right side that is the far node's Y, for a top/bottom side its X.
        private static double OrderKey(Side side, PortNode far)
        {
            return side is Side.Left or Side.Right ? far.CentreY : far.CentreX;
        }

        private static void Add(Dictionary<(int Node, Side Side), List<Port>> groups, (int Node, Side Side) key, Port port)
        {
            if (!groups.TryGetValue(key, out List<Port>? list))
            {
                list = [];
                groups[key] = list;
            }
            list.Add(port);
        }
    }
}
