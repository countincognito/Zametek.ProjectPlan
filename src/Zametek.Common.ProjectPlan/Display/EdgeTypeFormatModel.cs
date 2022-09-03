using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record EdgeTypeFormatModel
    {
        public EdgeType EdgeType { get; init; }

        public EdgeDashStyle EdgeDashStyle { get; init; }

        public EdgeWeightStyle EdgeWeightStyle { get; init; }
    }
}
