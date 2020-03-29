using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class ArrowGraphModel
    {
        public List<ActivityEdgeModel> Edges { get; set; }
        public List<EventNodeModel> Nodes { get; set; }
        public bool IsStale { get; set; }
    }
}
