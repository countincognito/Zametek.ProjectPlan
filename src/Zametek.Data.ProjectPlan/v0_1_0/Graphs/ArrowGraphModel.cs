using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class ArrowGraphModel
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<ActivityEdgeModel> Edges { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<EventNodeModel> Nodes { get; set; }

        public bool IsStale { get; set; }
    }
}
