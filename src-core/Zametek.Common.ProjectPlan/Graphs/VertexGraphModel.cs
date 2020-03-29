using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class VertexGraphModel
    {
        public List<EventEdgeModel> Edges { get; set; }
        public List<ActivityNodeModel> Nodes { get; set; }
    }
}
