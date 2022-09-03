namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ArrowGraphModel
    {
        public List<ActivityEdgeModel> Edges { get; init; } = new List<ActivityEdgeModel>();

        public List<EventNodeModel> Nodes { get; init; } = new List<EventNodeModel>();

        public bool IsStale { get; init; }
    }
}
