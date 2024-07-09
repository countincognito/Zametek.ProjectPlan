using Zametek.Maths.Graphs;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record ActivityNodeModel
    {
        public NodeType NodeType { get; init; }

        public ActivityModel? Content { get; init; }

        public List<int> IncomingEdges { get; init; } = [];

        public List<int> OutgoingEdges { get; init; } = [];
    }
}
