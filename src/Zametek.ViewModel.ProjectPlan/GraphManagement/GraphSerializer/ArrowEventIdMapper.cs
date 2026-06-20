using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    // Gives arrow-graph event nodes a stable id. The arrow compiler regenerates event ids on every
    // compile (a countdown counter, tied to nothing), so they cannot key a saved layout. Each event is
    // instead keyed by an incident activity, whose ids ARE stable: the lowest-id non-dummy activity
    // entering the event, or 0 for the Start node. An event with no non-dummy incoming edge keeps its
    // transient compiler id (best-effort: its position may not survive a recompile). Because each
    // activity has exactly one head event, the lowest non-dummy incoming id is unique across events.
    //
    // BuildEventIdLookup returns the old-id -> stable-id map (used when capturing a dragged layout);
    // Build applies it to relabel every event in the graph (used in CoreViewModel.BuildArrowGraph, so
    // the live graph, diagram, exports and persisted layout all share the stable id).
    internal static class ArrowEventIdMapper
    {
        private static bool TryMin(IEnumerable<int> ids, out int min)
        {
            min = int.MaxValue;
            bool any = false;
            foreach (int id in ids) { any = true; if (id < min) min = id; }
            return any;
        }

        public static Dictionary<int, int> BuildStableEventIdLookup(ArrowGraphModel arrowGraph)
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

        public static ArrowGraphModel ApplyStableIds(ArrowGraphModel arrowGraph)
        {
            ArgumentNullException.ThrowIfNull(arrowGraph);

            Dictionary<int, int> stableEventIdLookup = BuildStableEventIdLookup(arrowGraph);

            List<EventNodeModel> nodes = [.. arrowGraph.Nodes.Select(node =>
                {
                    int newId = node.Content.Id;

                    if (stableEventIdLookup.TryGetValue(newId, out int lookupId))
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
}
