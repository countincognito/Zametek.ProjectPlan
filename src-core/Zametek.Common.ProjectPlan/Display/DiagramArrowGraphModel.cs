using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class DiagramArrowGraphModel
    {
        public List<DiagramEdgeModel> Edges { get; set; }

        public List<DiagramNodeModel> Nodes { get; set; }
    }
}
