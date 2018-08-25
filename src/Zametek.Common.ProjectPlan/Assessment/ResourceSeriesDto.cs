using System;
using System.Collections.Generic;
using Zametek.Common.Project;
using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ResourceSeriesDto
    {
        public string Title { get; set; }
        public InterActivityAllocationType InterActivityAllocationType { get; set; }
        public List<int> Values { get; set; }
        public ColorFormatDto ColorFormatDto { get; set; }
        public double UnitCost { get; set; }
        public int DisplayOrder { get; set; }
    }
}
