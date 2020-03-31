using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class VertexGraphModel
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<EventEdgeModel> Edges { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<ActivityNodeModel> Nodes { get; set; }
    }
}
