using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class ArrowGraphModel
    {
        public List<ActivityEdgeModel> Edges { get; set; }

        public List<EventNodeModel> Nodes { get; set; }

        public bool IsStale { get; set; }
    }
}
