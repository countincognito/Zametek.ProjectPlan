namespace Zametek.Common.ProjectPlan
{
    // One node's persisted position in a GraphLayoutModel: its id and its layout-space coordinates.
    [Serializable]
    public record NodeLayoutModel
    {
        public int Id { get; init; }

        public double X { get; init; }

        public double Y { get; init; }
    }
}
