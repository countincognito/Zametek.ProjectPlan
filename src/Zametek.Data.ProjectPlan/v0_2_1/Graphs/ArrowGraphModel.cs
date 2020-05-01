using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class ArrowGraphModel
    {
        public List<ActivityEdgeModel> Edges { get; set; }

        public List<v0_1_0.EventNodeModel> Nodes { get; set; }

        public bool IsStale { get; set; }
    }
}
