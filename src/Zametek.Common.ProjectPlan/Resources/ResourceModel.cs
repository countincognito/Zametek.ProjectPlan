using System;
using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
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

        public int AllocationOrder { get; set; }

        public ColorFormatModel ColorFormat { get; set; }
    }
}
