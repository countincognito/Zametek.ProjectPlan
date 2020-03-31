using System;
using Zametek.Maths.Graphs;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class ResourceModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public bool IsExplicitTarget { get; set; }

        public InterActivityAllocationType InterActivityAllocationType { get; set; }

        public double UnitCost { get; set; }

        public int DisplayOrder { get; set; }

        public ColorFormatModel ColorFormat { get; set; }
    }
}
