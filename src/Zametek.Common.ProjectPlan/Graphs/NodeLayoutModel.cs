namespace Zametek.Common.ProjectPlan
{
    // One node's persisted position in a GraphLayoutModel: its key and its layout-space coordinates.
    // For a vertex node the key (Id) is the activity id. For an arrow event node the key is the lowest
    // activity id of the non-dummy incoming edges, or the lowest dummy incoming edge if there are no
    // non-dummy incoming edges, or 0 if it is the Start node.
    [Serializable]
    public record NodeLayoutModel
    {
        public int Id { get; init; }

        public double X { get; init; }

        public double Y { get; init; }
    }
}
