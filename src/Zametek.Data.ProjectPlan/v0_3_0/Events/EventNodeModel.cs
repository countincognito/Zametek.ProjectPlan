using Zametek.Maths.Graphs;

namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record EventNodeModel
    {
        public NodeType NodeType { get; init; }

        public v0_1_0.EventModel Content { get; init; } = new v0_1_0.EventModel();

        public List<int> IncomingEdges { get; init; } = [];

        public List<int> OutgoingEdges { get; init; } = [];
    }
}
