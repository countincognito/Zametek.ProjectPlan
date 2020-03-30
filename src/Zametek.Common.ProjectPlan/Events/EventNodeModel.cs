using System;
using System.Collections.Generic;
using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class EventNodeModel
    {
        public NodeType NodeType { get; set; }
        public EventModel Content { get; set; }
        public List<int> IncomingEdges { get; set; }
        public List<int> OutgoingEdges { get; set; }
    }
}
