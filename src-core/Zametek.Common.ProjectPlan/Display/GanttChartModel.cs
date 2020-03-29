using System;
using System.Collections.Generic;
using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class GanttChartModel
    {
        public IList<IDependentActivity<int, int>> DependentActivities { get; set; }

        public IList<IResourceSchedule<int, int>> ResourceSchedules { get; set; }

        public IList<ResourceSeriesModel> ResourceSeriesSet { get; set; }

        public bool IsStale { get; set; }
    }
}
