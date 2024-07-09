using Zametek.Maths.Graphs;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record EventNodeModel
    {
        public NodeType NodeType { get; init; }

        public EventModel? Content { get; init; }

        public List<int> IncomingEdges { get; init; } = [];

        public List<int> OutgoingEdges { get; init; } = [];
    }
}
