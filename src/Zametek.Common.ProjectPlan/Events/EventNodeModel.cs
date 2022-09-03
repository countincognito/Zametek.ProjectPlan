using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record EventNodeModel
    {
        public NodeType NodeType { get; init; }

        public EventModel Content { get; init; } = new EventModel();

        public List<int> IncomingEdges { get; init; } = new List<int>();

        public List<int> OutgoingEdges { get; init; } = new List<int>();
    }
}
