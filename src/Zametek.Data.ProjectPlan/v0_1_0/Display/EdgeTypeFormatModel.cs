using Zametek.Maths.Graphs;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record EdgeTypeFormatModel
    {
        public EdgeType EdgeType { get; init; }

        public EdgeDashStyle EdgeDashStyle { get; init; }

        public EdgeWeightStyle EdgeWeightStyle { get; init; }
    }
}
