using System.Collections.Generic;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IAssessingEngine
    {
        MetricsDto CalculateProjectMetrics(IList<IActivity<int>> activities, IList<ActivitySeverityDto> activitySeverityDtos);
        IList<ResourceSeriesDto> CalculateResourceSeriesSet(IList<IResourceSchedule<int>> resourceSchedules, IList<ResourceDto> resources, double defaultUnitCost);
        CostsDto CalculateProjectCosts(IList<ResourceSeriesDto> resourceSeriesSet);
    }
}
