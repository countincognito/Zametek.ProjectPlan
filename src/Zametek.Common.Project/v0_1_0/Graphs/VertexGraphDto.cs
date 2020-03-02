using System;
using System.Collections.Generic;

namespace Zametek.Common.Project.v0_1_0
{
    [Serializable]
    public class VertexGraphDto
    {
        public List<EventEdgeDto> Edges { get; set; }
        public List<ActivityNodeDto> Nodes { get; set; }
    }
}
