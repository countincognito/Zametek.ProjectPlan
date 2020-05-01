using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class VertexGraphModel
    {
        public List<v0_1_0.EventEdgeModel> Edges { get; set; }

        public List<ActivityNodeModel> Nodes { get; set; }
    }
}
