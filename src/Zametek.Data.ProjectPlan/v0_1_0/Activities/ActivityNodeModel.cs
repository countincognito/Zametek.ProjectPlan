using System;
using System.Collections.Generic;
using Zametek.Maths.Graphs;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class ActivityNodeModel
    {
        public NodeType NodeType { get; set; }

        public ActivityModel Content { get; set; }

        public List<int> IncomingEdges { get; set; }

        public List<int> OutgoingEdges { get; set; }
    }
}
