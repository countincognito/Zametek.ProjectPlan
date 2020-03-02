using System;
using Zametek.Maths.Graphs;

namespace Zametek.Common.Project.v0_1_0
{
    [Serializable]
    public class EdgeTypeFormatDto
    {
        public EdgeType EdgeType { get; set; }
        public EdgeDashStyle EdgeDashStyle { get; set; }
        public EdgeWeightStyle EdgeWeightStyle { get; set; }
    }
}
