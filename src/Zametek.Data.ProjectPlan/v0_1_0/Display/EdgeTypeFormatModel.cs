using System;
using Zametek.Maths.Graphs;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class EdgeTypeFormatModel
    {
        public EdgeType EdgeType { get; set; }

        public EdgeDashStyle EdgeDashStyle { get; set; }

        public EdgeWeightStyle EdgeWeightStyle { get; set; }
    }
}
