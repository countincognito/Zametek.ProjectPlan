using System;
using System.Collections.Generic;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    [Serializable]
    public class GanttChartDto
    {
        public IList<IDependentActivity<int>> DependentActivities { get; set; }

        public IList<IResourceSchedule<int>> ResourceSchedules { get; set; }

        public IList<ResourceSeriesDto> ResourceSeriesSet { get; set; }

        public bool IsStale { get; set; }
    }
}
