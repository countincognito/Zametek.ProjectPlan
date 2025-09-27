namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record EventNodeModel
    {
        public Maths.Graphs.NodeType NodeType { get; init; }

        public EventModel Content { get; init; } = new EventModel();

        public List<int> IncomingEdges { get; init; } = [];

        public List<int> OutgoingEdges { get; init; } = [];
    }
}
