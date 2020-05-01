using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class VertexGraphModel
    {
        public List<EventEdgeModel> Edges { get; set; }

        public List<ActivityNodeModel> Nodes { get; set; }
    }
}
