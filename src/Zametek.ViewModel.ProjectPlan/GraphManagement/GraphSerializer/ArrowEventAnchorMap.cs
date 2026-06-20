using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    internal static class ArrowEventAnchorMap
    {
        private static bool TryMin(IEnumerable<int> ids, out int min)
        {
            min = int.MaxValue;
            bool any = false;
            foreach (int id in ids) { any = true; if (id < min) min = id; }
            return any;
        }

        public static Dictionary<int, int> BuildEventIdLookup(ArrowGraphModel arrowGraph)
        {
            ArgumentNullException.ThrowIfNull(arrowGraph);

            // edge id -> is dummy? (IsDummy is semantic, so this never relies on the id's sign)
            Dictionary<int, bool> isDummyByEdgeId =
                arrowGraph.Edges.ToDictionary(e => e.Content.Id, e => e.IsDummy);

            var byEventId = new Dictionary<int, int>();

            foreach (EventNodeModel node in arrowGraph.Nodes)
            {
                int eventId = node.Content.Id;

                if (node.NodeType == Maths.Graphs.NodeType.Start)
                {
                    byEventId[eventId] = 0;
                }
                else
                {
                    IEnumerable<int> realIncoming =
                        node.IncomingEdges.Where(id => !isDummyByEdgeId.GetValueOrDefault(id, false));

                    int newId = TryMin(realIncoming, out int inMin) ? inMin : eventId;
                    byEventId[eventId] = newId;
                }
            }

            return byEventId;
        }

        public static ArrowGraphModel Build(ArrowGraphModel arrowGraph)
        {
            ArgumentNullException.ThrowIfNull(arrowGraph);

            Dictionary<int, int> eventIdLookup = BuildEventIdLookup(arrowGraph);

            List<EventNodeModel> nodes = [.. arrowGraph.Nodes.Select(node =>
                {
                    int newId = node.Content.Id;

                    if (eventIdLookup.TryGetValue(newId, out int lookupId))
                    {
                        newId = lookupId;
                    }

                    return node with
                    {
                        Content = node.Content with
                        {
                            Id = newId
                        }
                    };
                })];

            return arrowGraph with { Nodes = nodes };
        }
    }

























    //    // Gives arrow-graph event nodes a stable id. The arrow compiler regenerates event ids on every
    //    // compile (a countdown counter, tied to nothing), but activity (edge) ids are stable, so each
    //    // event is anchored to an incident activity: the lowest-id real (non-dummy) activity entering it
    //    // (Head), or - for an event nothing real enters, such as the start event - the lowest-id real
    //    // activity leaving it (Tail). Each activity has exactly one head event and one tail event, so an
    //    // (activity id, side) pair identifies an event uniquely and reproduces on an unchanged recompile.
    //    //
    //    // Relabel() stamps that stable id onto the live graph in CoreViewModel.BuildArrowGraph, so the
    //    // interactive view, exports and persisted layout all key off it directly. The persisted layout
    //    // stores the durable (activity id, side) pair (NodeLayoutModel.Id / .Anchor) and maps to/from the
    //    // live event id by Encode/Decode alone - never consulting the graph - so seeding works on load,
    //    // before the graph the layout belongs to has been rebuilt.
    //    internal static class ArrowEventAnchorMap
    //    {
    //        // Stamp a stable, anchor-derived id onto every event node, replacing the compiler's transient
    //        // ids. Topology is carried by the nodes' edge-id lists (activity ids, untouched here), so only
    //        // EventModel.Id is rewritten. A pure-dummy event - which arrow graphs do not produce - has no
    //        // anchor and keeps its transient id (its position simply will not persist).
    //        public static ArrowGraphModel Relabel(ArrowGraphModel arrowGraph)
    //        {
    //            ArgumentNullException.ThrowIfNull(arrowGraph);

    //            Dictionary<int, bool> isDummyByEdgeId =
    //                arrowGraph.Edges.ToDictionary(e => e.Content.Id, e => e.Content.IsDummy());

    //            List<EventNodeModel> nodes = [.. arrowGraph.Nodes.Select(node =>
    //            {
    //                ArrowEventAnchor? anchor = ResolveAnchor(node, isDummyByEdgeId);
    //                return anchor is { } resolved
    //                    ? node with { Content = node.Content with { Id = Encode(resolved) } }
    //                    : node;
    //            })];

    //            return arrowGraph with { Nodes = nodes };
    //        }

    //        // Capture: translate the interactive graph's live node positions (keyed by the stable event id
    //        // Relabel stamped) into a persisted layout keyed by the durable (activity id, side) anchor.
    //        public static CommonGraphLayoutModel ToAnchoredLayout(IReadOnlyList<GraphNodePosition> positions)
    //        {
    //            ArgumentNullException.ThrowIfNull(positions);
    //            return new CommonGraphLayoutModel
    //            {
    //                Nodes = [.. positions.Select(p =>
    //                {
    //                    ArrowEventAnchor anchor = Decode(p.Id);
    //                    return new NodeLayoutModel { Id = anchor.ActivityId, Anchor = anchor.Side, X = p.X, Y = p.Y };
    //                })],
    //            };
    //        }

    //        // Seed: translate a persisted (anchor-keyed) layout into live node positions for the current
    //        // compile. Pure - no graph needed - because the live id is Encode(anchor), the same value
    //        // Relabel stamps. Entries that are not arrow anchors (e.g. a stray Self) are skipped.
    //        public static IReadOnlyList<GraphNodePosition> ToSeedPositions(CommonGraphLayoutModel layout)
    //        {
    //            ArgumentNullException.ThrowIfNull(layout);
    //            var positions = new List<GraphNodePosition>(layout.Nodes.Count);
    //            foreach (NodeLayoutModel node in layout.Nodes)
    //            {
    //                if (node.Anchor is AnchorSide.Head or AnchorSide.Tail)
    //                {
    //                    int liveEventId = Encode(new ArrowEventAnchor(node.Id, node.Anchor));
    //                    positions.Add(new GraphNodePosition(liveEventId, node.X, node.Y));
    //                }
    //            }
    //            return positions;
    //        }

    //        // Head if a real activity terminates here (covers the end event); otherwise Tail by the lowest
    //        // real activity leaving here (covers the start event). A dummy is a zero-duration activity
    //        // (ModelExtensions.IsDummy) - the same rule the diagram builder uses, never the id's sign.
    //        private static ArrowEventAnchor? ResolveAnchor(EventNodeModel node, IReadOnlyDictionary<int, bool> isDummyByEdgeId)
    //        {
    //            if (TryMinReal(node.IncomingEdges, isDummyByEdgeId, out int inMin))
    //            {
    //                return new ArrowEventAnchor(inMin, AnchorSide.Head);
    //            }
    //            if (TryMinReal(node.OutgoingEdges, isDummyByEdgeId, out int outMin))
    //            {
    //                return new ArrowEventAnchor(outMin, AnchorSide.Tail);
    //            }
    //            return null;
    //        }

    //        private static bool TryMinReal(IReadOnlyList<int> edgeIds, IReadOnlyDictionary<int, bool> isDummyByEdgeId, out int min)
    //        {
    //            min = int.MaxValue;
    //            bool any = false;
    //            foreach (int id in edgeIds)
    //            {
    //                if (isDummyByEdgeId.GetValueOrDefault(id, false))
    //                {
    //                    continue;
    //                }
    //                any = true;
    //                if (id < min)
    //                {
    //                    min = id;
    //                }
    //            }
    //            return any;
    //        }

    //        // Pack an (activity id, side) anchor into a single stable event id and back. Parity carries the
    //        // side (Head even, Tail odd), so the pairing is injective for any sign of activity id and is
    //        // unique across the graph's events (one head + one tail per activity). Activity ids sit
    //        // comfortably within +/-2^30, so doubling cannot overflow in practice. The encoding is an
    //        // internal runtime detail - the file persists the durable (activity id, side) pair instead.
    //        private static int Encode(ArrowEventAnchor anchor)
    //        {
    //            int doubled = anchor.ActivityId * 2;
    //            return anchor.Side == AnchorSide.Tail ? doubled + 1 : doubled;
    //        }

    //        private static ArrowEventAnchor Decode(int eventId)
    //        {
    //            AnchorSide side = (eventId & 1) == 0 ? AnchorSide.Head : AnchorSide.Tail;
    //            return new ArrowEventAnchor(eventId >> 1, side);
    //        }
    //    }

    //    // The stable identity of an arrow-graph event node: an incident activity id plus which side of that
    //    // activity the event sits on. Persisted as (NodeLayoutModel.Id, NodeLayoutModel.Anchor).
    //    internal readonly record struct ArrowEventAnchor(int ActivityId, AnchorSide Side);
}
