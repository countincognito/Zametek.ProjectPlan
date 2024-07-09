using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ActivityNodeModel
    {
        public NodeType NodeType { get; init; }

        public ActivityModel Content { get; init; } = new ActivityModel();

        public List<int> IncomingEdges { get; init; } = [];

        public List<int> OutgoingEdges { get; init; } = [];
    }
}
