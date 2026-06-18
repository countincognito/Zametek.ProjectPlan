namespace Zametek.Graphs.ProjectPlan
{
    // A node in a port-resolution snapshot: just its id and centre (screen coordinates).
    internal readonly record struct PortNode(int Id, double CentreX, double CentreY);

    // An edge in a port-resolution snapshot: its id, its source/target node ids, and the per-endpoint
    // connection axes it would use on its own (before de-confliction).
    internal readonly record struct PortEdge(
        int Id,
        int SourceId,
        int TargetId,
        GraphConnectionAxis SourceAxis,
        GraphConnectionAxis TargetAxis);

    // Adjusts the per-edge connection axes so that, at any node, no single side carries BOTH an
    // incoming and an outgoing edge. Two incoming edges on a side is fine, and two outgoing on a side
    // is fine - only a mix of an arrival and a departure on the same side is disallowed (their
    // arrowheads would sit on top of each other). Used only by the rectilinear families, and only for
    // the drag-time approximation (at rest the exact MSAGL route already assigns sensible ports).
    //
    // The side an edge uses at a node follows its near-end axis: Horizontal -> the left/right side
    // facing the far node, Vertical -> the top/bottom side. To move an edge off a conflicted side we
    // "flip" it: an L route (mixed axes) is rotated by swapping its two axes - which moves BOTH its
    // attach points, so the edge also leaves its other node from a different side; a Z route (matching
    // axes) is turned into an L by toggling only its near-end axis.
    //
    // Resolution rule (preferring the cleaner in-line "Z" route): on a conflicted side, the direction
    // whose edges include a Z route is kept and the other direction yields; if neither (or both)
    // direction has a Z route, the incoming edges yield. The pass iterates to convergence - a flip can
    // push the conflict to the far node, which the next iteration resolves - with each edge flipped at
    // most once, so it always terminates (any residual in a rare flip cycle is left for the drop-time
    // MSAGL reroute to clean up).
    internal static class GraphPortResolver
    {
        private enum Side
        {
            Left,
            Right,
            Top,
            Bottom,
        }

        private static readonly Side[] s_Sides = [Side.Left, Side.Right, Side.Top, Side.Bottom];

        public static IReadOnlyDictionary<int, (GraphConnectionAxis Source, GraphConnectionAxis Target)> Resolve(
            IReadOnlyList<PortNode> nodes,
            IReadOnlyList<PortEdge> edges)
        {
            var axes = new Dictionary<int, (GraphConnectionAxis Source, GraphConnectionAxis Target)>(edges.Count);
            var edgeById = new Dictionary<int, PortEdge>(edges.Count);
            var centreById = new Dictionary<int, PortNode>(nodes.Count);
            foreach (PortNode node in nodes)
            {
                centreById[node.Id] = node;
            }

            var incident = new Dictionary<int, List<int>>();
            foreach (PortEdge edge in edges)
            {
                axes[edge.Id] = (edge.SourceAxis, edge.TargetAxis);
                edgeById[edge.Id] = edge;

                // Skip self-loops and edges referencing a missing node: they cannot create an
                // incoming/outgoing conflict and keep their own axes.
                if (edge.SourceId == edge.TargetId
                    || !centreById.ContainsKey(edge.SourceId)
                    || !centreById.ContainsKey(edge.TargetId))
                {
                    continue;
                }
                AddIncident(incident, edge.SourceId, edge.Id);
                AddIncident(incident, edge.TargetId, edge.Id);
            }

            var flipped = new HashSet<int>();
            int cap = edges.Count + 1;
            bool changed = true;
            for (int iteration = 0; changed && iteration < cap; iteration++)
            {
                changed = false;
                foreach (PortNode node in nodes)
                {
                    if (incident.TryGetValue(node.Id, out List<int>? incidentEdges)
                        && ResolveNode(node, incidentEdges, edgeById, centreById, axes, flipped))
                    {
                        changed = true;
                    }
                }
            }
            return axes;
        }

        // Resolve every conflicted side of one node, flipping the yielding edges (each at most once).
        // Returns whether anything was flipped. Groups are read from the current axes; a flip takes
        // effect on the next outer iteration.
        private static bool ResolveNode(
            PortNode node,
            List<int> incidentEdges,
            Dictionary<int, PortEdge> edgeById,
            Dictionary<int, PortNode> centreById,
            Dictionary<int, (GraphConnectionAxis Source, GraphConnectionAxis Target)> axes,
            HashSet<int> flipped)
        {
            bool any = false;
            foreach (Side side in s_Sides)
            {
                var incoming = new List<int>();
                var outgoing = new List<int>();
                foreach (int id in incidentEdges)
                {
                    PortEdge edge = edgeById[id];
                    bool isOutgoing = edge.SourceId == node.Id;
                    if (SideAt(node, edge, isOutgoing, axes[id], centreById) != side)
                    {
                        continue;
                    }
                    (isOutgoing ? outgoing : incoming).Add(id);
                }

                if (incoming.Count == 0 || outgoing.Count == 0)
                {
                    continue;
                }

                List<int> victims = YieldIncoming(incoming, outgoing, axes) ? incoming : outgoing;
                foreach (int victim in victims)
                {
                    if (flipped.Add(victim))
                    {
                        Flip(victim, node.Id, edgeById[victim], axes);
                        any = true;
                    }
                }
            }
            return any;
        }

        // The side an edge attaches to at the given node, from its near-end axis and the direction to
        // the far node (matching GraphEdgeGeometry.AttachPoint).
        private static Side SideAt(
            PortNode node,
            PortEdge edge,
            bool isOutgoing,
            (GraphConnectionAxis Source, GraphConnectionAxis Target) edgeAxes,
            Dictionary<int, PortNode> centreById)
        {
            GraphConnectionAxis nearAxis = isOutgoing ? edgeAxes.Source : edgeAxes.Target;
            PortNode far = centreById[isOutgoing ? edge.TargetId : edge.SourceId];
            if (nearAxis == GraphConnectionAxis.Horizontal)
            {
                return far.CentreX >= node.CentreX ? Side.Right : Side.Left;
            }
            return far.CentreY >= node.CentreY ? Side.Bottom : Side.Top;
        }

        // Keep the direction whose edges include a Z route (the preferred in-line through-route); if
        // neither or both directions have a Z route, the incoming edges yield.
        private static bool YieldIncoming(
            List<int> incoming,
            List<int> outgoing,
            Dictionary<int, (GraphConnectionAxis Source, GraphConnectionAxis Target)> axes)
        {
            bool incomingHasZ = incoming.Any(id => IsZ(axes[id]));
            bool outgoingHasZ = outgoing.Any(id => IsZ(axes[id]));
            if (outgoingHasZ && !incomingHasZ)
            {
                return true;
            }
            if (incomingHasZ && !outgoingHasZ)
            {
                return false;
            }
            return true;
        }

        // Move a victim edge off its current side: an L route is rotated (swap axes -> both ends move);
        // a Z route is turned into an L by toggling only the axis at this node.
        private static void Flip(
            int edgeId,
            int nodeId,
            PortEdge edge,
            Dictionary<int, (GraphConnectionAxis Source, GraphConnectionAxis Target)> axes)
        {
            (GraphConnectionAxis source, GraphConnectionAxis target) = axes[edgeId];
            if (source != target)
            {
                axes[edgeId] = (target, source);
                return;
            }
            axes[edgeId] = edge.TargetId == nodeId
                ? (source, Opposite(target))
                : (Opposite(source), target);
        }

        private static bool IsZ((GraphConnectionAxis Source, GraphConnectionAxis Target) axes)
        {
            return axes.Source == axes.Target;
        }

        private static GraphConnectionAxis Opposite(GraphConnectionAxis axis)
        {
            return axis == GraphConnectionAxis.Horizontal
                ? GraphConnectionAxis.Vertical
                : GraphConnectionAxis.Horizontal;
        }

        private static void AddIncident(Dictionary<int, List<int>> incident, int nodeId, int edgeId)
        {
            if (!incident.TryGetValue(nodeId, out List<int>? list))
            {
                list = [];
                incident[nodeId] = list;
            }
            list.Add(edgeId);
        }
    }
}
