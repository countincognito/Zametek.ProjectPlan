using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ArrowGraphModel
    {
        public List<ActivityEdgeModel> Edges { get; set; }
        public List<EventNodeModel> Nodes { get; set; }
        public bool IsStale { get; set; }
    }
}
