using System;
using System.Collections.Generic;
using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ActivityNodeModel
    {
        public NodeType NodeType { get; set; }

        public ActivityModel Content { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<int> IncomingEdges { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<int> OutgoingEdges { get; set; }
    }
}
