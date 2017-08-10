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
        private readonly IMetricAssessingEngine m_MetricAssessingEngine;

        #endregion

        #region Ctors

        public ProjectManager(
            IGraphProcessingEngine graphProcessingEngine,
            IMetricAssessingEngine metricAssessingEngine)
        {
            m_GraphProcessingEngine = graphProcessingEngine ?? throw new ArgumentNullException(nameof(graphProcessingEngine));
            m_MetricAssessingEngine = metricAssessingEngine ?? throw new ArgumentNullException(nameof(metricAssessingEngine));
        }

        #endregion

        #region IProjectManager Members

        public MetricsDto CalculateProjectMetrics(IList<IActivity<int>> activities, IList<ActivitySeverityDto> activitySeverityDtos)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            if (activitySeverityDtos == null)
            {
                throw new ArgumentNullException(nameof(activitySeverityDtos));
            }
            return m_MetricAssessingEngine.CalculateProjectMetrics(activities, activitySeverityDtos);
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
