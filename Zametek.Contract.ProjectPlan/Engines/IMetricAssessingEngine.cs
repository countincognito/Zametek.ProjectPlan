using System.Collections.Generic;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IMetricAssessingEngine
    {
        MetricsDto CalculateProjectMetrics(IList<IActivity<int>> activities, IList<ActivitySeverityDto> activitySeverityDtos);
    }
}
