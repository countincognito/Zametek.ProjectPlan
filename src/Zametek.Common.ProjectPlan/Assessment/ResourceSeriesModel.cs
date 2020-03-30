using System;
using System.Collections.Generic;
using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ResourceSeriesModel
    {
        public string Title { get; set; }
        public InterActivityAllocationType InterActivityAllocationType { get; set; }
        public List<int> Values { get; set; }
        public ColorFormatModel ColorFormat { get; set; }
        public double UnitCost { get; set; }
        public int DisplayOrder { get; set; }
    }
}
