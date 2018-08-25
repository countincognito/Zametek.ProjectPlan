using System;
using System.Collections.Generic;

namespace Zametek.Common.Project
{
    [Serializable]
    public class ArrowGraphDto
    {
        public List<ActivityEdgeDto> Edges { get; set; }
        public List<EventNodeDto> Nodes { get; set; }
        public bool IsStale { get; set; }
    }
}
