using System;
using System.Collections.Generic;
using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ResourceSeriesModel
    {
        public int? ResourceId { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public string Title { get; set; }

        public InterActivityAllocationType InterActivityAllocationType { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<int> Values { get; set; }

        public ResourceScheduleModel ResourceSchedule { get; set; }

        public ColorFormatModel ColorFormat { get; set; }

        public double UnitCost { get; set; }

        public int DisplayOrder { get; set; }
    }
}
