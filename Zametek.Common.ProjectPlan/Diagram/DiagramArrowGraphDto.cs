using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class DiagramArrowGraphDto
    {
        public IList<DiagramEdgeDto> Edges { get; set; }
        public IList<DiagramNodeDto> Nodes { get; set; }
    }
}
