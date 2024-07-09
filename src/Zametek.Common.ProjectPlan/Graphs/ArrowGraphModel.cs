namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ArrowGraphModel
    {
        public List<ActivityEdgeModel> Edges { get; init; } = [];

        public List<EventNodeModel> Nodes { get; init; } = [];

        public bool IsStale { get; init; }
    }
}
