using System.Collections.Generic;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectService
    {
        CostsModel CalculateProjectCosts(IEnumerable<ResourceSeriesModel> resourceSeriesSet);

        MetricsModel CalculateProjectMetrics(IEnumerable<IActivity<int, int>> activities, IEnumerable<ActivitySeverityModel> activitySeverities);

        IEnumerable<ResourceSeriesModel> CalculateResourceSeriesSet(IEnumerable<IResourceSchedule<int, int>> resourceSchedules, IEnumerable<ResourceModel> resources, double defaultUnitCost);

        byte[] ExportArrowGraphToDiagram(DiagramArrowGraphModel diagramArrowGraph);
    }
}
