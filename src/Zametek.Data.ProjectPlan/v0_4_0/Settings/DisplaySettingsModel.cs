using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.v0_4_0
{
    [Serializable]
    public record DisplaySettingsModel
    {
        public bool ViewEarnedValueProjections { get; init; }

        public GroupByMode GanttChartGroupByMode { get; init; }

        public AnnotationStyle GanttChartAnnotationStyle { get; init; }

        public bool ViewGanttChartGroupLabels { get; init; }

        public bool ViewGanttChartProjectFinish { get; init; }

        public bool ViewGanttChartTracking { get; init; }
    }
}
