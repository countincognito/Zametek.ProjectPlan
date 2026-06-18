using Avalonia;

namespace Zametek.Graphs.ProjectPlan
{
    // Spreads the edges that attach to the same node side apart so their ports (and arrowheads) do not
    // overlap. An incoming and an outgoing edge may share a side; every edge attaching to a side is
    // simply offset along it. Used only by the rectilinear families and only for the drag-time
    // approximation. Returns a per-edge perpendicular attach-point offset for each end (zero when an end
    // is the only edge on its side).
    //
    // The side an edge actually attaches on is taken from its resolved attach point (PortPlacement),
    // NOT re-derived from the axis + the other node's direction - so the detour shapes (Bracket /
    // Saucepan), whose ends can leave on a side facing away from the far node, are grouped correctly.
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
            IReadOnlyList<PortPlacement> placements,
            double nodeWidth,
            double nodeHeight)
        {
            var centreById = new Dictionary<int, PortNode>(nodes.Count);
            foreach (PortNode node in nodes)
            {
                centreById[node.Id] = node;
            }
            double halfWidth = nodeWidth / 2.0;
            double halfHeight = nodeHeight / 2.0;

            var result = new Dictionary<int, (Point SourceOffset, Point TargetOffset)>(placements.Count);
            var groups = new Dictionary<(int Node, Side Side), List<Port>>();
            foreach (PortPlacement placement in placements)
            {
                result[placement.EdgeId] = (default, default);
                if (placement.SourceId == placement.TargetId
                    || !centreById.TryGetValue(placement.SourceId, out PortNode sourceNode)
                    || !centreById.TryGetValue(placement.TargetId, out PortNode targetNode))
                {
                    continue;
                }
                Side sourceSide = SideOf(sourceNode, placement.SourceAttach, halfWidth, halfHeight);
                Add(groups, (placement.SourceId, sourceSide), new Port(placement.EdgeId, IsSource: true, OrderKey(sourceSide, targetNode)));
                Side targetSide = SideOf(targetNode, placement.TargetAttach, halfWidth, halfHeight);
                Add(groups, (placement.TargetId, targetSide), new Port(placement.EdgeId, IsSource: false, OrderKey(targetSide, sourceNode)));
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

        // The node border the attach point sits on, from which extent it has reached: a left/right side
        // when the attach is (about) half a width off-centre horizontally, a top/bottom side when it is
        // half a height off vertically. Works for any shape because it reads the actual attach point.
        private static Side SideOf(PortNode node, Point attach, double halfWidth, double halfHeight)
        {
            double horizontalResidual = halfWidth - Math.Abs(attach.X - node.CentreX);
            double verticalResidual = halfHeight - Math.Abs(attach.Y - node.CentreY);
            if (horizontalResidual <= verticalResidual)
            {
                return attach.X >= node.CentreX ? Side.Right : Side.Left;
            }
            return attach.Y >= node.CentreY ? Side.Bottom : Side.Top;
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
