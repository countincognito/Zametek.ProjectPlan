namespace Zametek.Common.ProjectPlan
{
    // One node's persisted position in a GraphLayoutModel: its key and its layout-space coordinates.
    // For a vertex node the key (Id) is the activity id. For an arrow event node the key is the lowest
    // non-dummy incoming activity id, or 0 for the Start node; an event with no non-dummy incoming edge
    // keeps its transient compiler id, so its position is not reliably restored across recompiles.
    [Serializable]
    public record NodeLayoutModel
    {
        public int Id { get; init; }

        public double X { get; init; }

        public double Y { get; init; }
    }
}
