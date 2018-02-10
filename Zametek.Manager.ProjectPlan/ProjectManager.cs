using System;
using System.Collections.Generic;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Manager.ProjectPlan
{
    public class ProjectManager
        : IProjectManager
    {
        #region Fields

        private readonly IGraphProcessingEngine m_GraphProcessingEngine;
        private readonly IAssessingEngine m_AssessingEngine;

        #endregion

        #region Ctors

        public ProjectManager(
            IGraphProcessingEngine graphProcessingEngine,
            IAssessingEngine metricAssessingEngine)
        {
            m_GraphProcessingEngine = graphProcessingEngine ?? throw new ArgumentNullException(nameof(graphProcessingEngine));
            m_AssessingEngine = metricAssessingEngine ?? throw new ArgumentNullException(nameof(metricAssessingEngine));
        }

        #endregion

        #region IProjectManager Members

        public MetricsDto CalculateProjectMetrics(
            IList<IActivity<int>> activities,
            IList<ActivitySeverityDto> activitySeverityDtos)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            if (activitySeverityDtos == null)
            {
                throw new ArgumentNullException(nameof(activitySeverityDtos));
            }
            return m_AssessingEngine.CalculateProjectMetrics(activities, activitySeverityDtos);
        }

        public IList<ResourceSeriesDto> CalculateResourceSeriesSet(
            IList<IResourceSchedule<int>> resourceSchedules,
            IList<ResourceDto> resources,
            double defaultUnitCost)
        {
            if (resourceSchedules == null)
            {
                throw new ArgumentNullException(nameof(resourceSchedules));
            }
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }
            return m_AssessingEngine.CalculateResourceSeriesSet(resourceSchedules, resources, defaultUnitCost);
        }

        public CostsDto CalculateProjectCosts(IList<ResourceSeriesDto> resourceSeriesSet)
        {
            if (resourceSeriesSet == null)
            {
                throw new ArgumentNullException(nameof(resourceSeriesSet));
            }
            return m_AssessingEngine.CalculateProjectCosts(resourceSeriesSet);
        }

        public byte[] ExportArrowGraphToDiagram(DiagramArrowGraphDto diagramArrowGraphDto)
        {
            if (diagramArrowGraphDto == null)
            {
                throw new ArgumentNullException(nameof(diagramArrowGraphDto));
            }
            return m_GraphProcessingEngine.ExportArrowGraphToDiagram(diagramArrowGraphDto);
        }

        #endregion
    }
}
