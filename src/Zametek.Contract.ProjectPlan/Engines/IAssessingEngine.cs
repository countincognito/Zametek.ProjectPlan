using System.Collections.Generic;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IAssessingEngine
    {
        MetricsDto CalculateProjectMetrics(IList<IActivity<int>> activities, IList<Common.Project.v0_1_0.ActivitySeverityDto> activitySeverityDtos);
        IList<ResourceSeriesDto> CalculateResourceSeriesSet(IList<IResourceSchedule<int>> resourceSchedules, IList<Common.Project.v0_1_0.ResourceDto> resources, double defaultUnitCost);
        CostsDto CalculateProjectCosts(IList<ResourceSeriesDto> resourceSeriesSet);
    }
}
