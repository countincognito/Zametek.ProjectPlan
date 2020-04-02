using System.Collections.Generic;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectService
    {
        CostsModel CalculateProjectCosts(ResourceSeriesSetModel resourceSeriesSet);

        MetricsModel CalculateProjectMetrics(IEnumerable<ActivityModel> activities, IEnumerable<ActivitySeverityModel> activitySeverities);

        ResourceSeriesSetModel CalculateResourceSeriesSet(IEnumerable<ResourceScheduleModel> resourceSchedules, IEnumerable<ResourceModel> resources, double defaultUnitCost);

        byte[] ExportArrowGraphToDiagram(DiagramArrowGraphModel diagramArrowGraph);
    }
}
