using System;
using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class EdgeTypeFormatModel
    {
        public EdgeType EdgeType { get; set; }

        public EdgeDashStyle EdgeDashStyle { get; set; }

        public EdgeWeightStyle EdgeWeightStyle { get; set; }
    }
}
