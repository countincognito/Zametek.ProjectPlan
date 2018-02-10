using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class DiagramArrowGraphDto
    {
        public List<DiagramEdgeDto> Edges { get; set; }
        public List<DiagramNodeDto> Nodes { get; set; }
    }
}
