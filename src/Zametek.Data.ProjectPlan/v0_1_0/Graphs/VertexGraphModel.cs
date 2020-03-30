using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class VertexGraphModel
    {
        public List<EventEdgeModel> Edges { get; set; }
        public List<ActivityNodeModel> Nodes { get; set; }
    }
}
