using System;
using System.Collections.Generic;
using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class EventNodeModel
    {
        public NodeType NodeType { get; set; }

        public EventModel Content { get; set; }

        public List<int> IncomingEdges { get; set; }

        public List<int> OutgoingEdges { get; set; }
    }
}
