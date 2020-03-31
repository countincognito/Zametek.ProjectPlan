using System;
using System.Collections.Generic;
using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class GanttChartModel
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public IList<IDependentActivity<int, int>> DependentActivities { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public IList<IResourceSchedule<int, int>> ResourceSchedules { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public IList<ResourceSeriesModel> ResourceSeriesSet { get; set; }

        public bool IsStale { get; set; }
    }
}
