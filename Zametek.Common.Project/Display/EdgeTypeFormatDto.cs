using System;
using Zametek.Maths.Graphs;

namespace Zametek.Common.Project
{
    [Serializable]
    public class EdgeTypeFormatDto
    {
        public EdgeType EdgeType { get; set; }
        public EdgeDashStyle EdgeDashStyle { get; set; }
        public EdgeWeightStyle EdgeWeightStyle { get; set; }
    }
}
