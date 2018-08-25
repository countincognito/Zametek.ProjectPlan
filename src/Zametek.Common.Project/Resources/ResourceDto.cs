using System;
using Zametek.Maths.Graphs;

namespace Zametek.Common.Project
{
    [Serializable]
    public class ResourceDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsExplicitTarget { get; set; }
        public InterActivityAllocationType InterActivityAllocationType { get; set; }
        public double UnitCost { get; set; }
        public int DisplayOrder { get; set; }
        public ColorFormatDto ColorFormat { get; set; }
    }
}
