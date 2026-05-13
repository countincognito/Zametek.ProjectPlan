using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public record ProjectScenarioDisplaySettingsModel
    {
        public bool ShowDates { get; init; } = default;

        public bool UseClassicDates { get; init; } = default;

        public NonWorkingDayMode NonWorkingDayMode { get; init; } = default;

        public bool HideCost { get; init; } = default;

        public bool HideBilling { get; init; } = default;



        public bool ArrowGraphShowNames { get; init; } = default;



        public bool VertexGraphShowNames { get; init; } = default;



        public GroupByMode GanttChartGroupByMode { get; init; } = default;

        public AnnotationStyle GanttChartAnnotationStyle { get; init; } = default;

        public bool GanttChartShowGroupLabels { get; init; } = default;

        public bool GanttChartShowProjectFinish { get; init; } = default;

        public bool GanttChartShowTracking { get; init; } = default;

        public bool GanttChartShowToday { get; init; } = default;

        public bool GanttChartShowMilestones { get; init; } = default;

        public bool GanttChartShowSlack { get; init; } = default;

        public bool GanttChartShowNonWorkingDays { get; init; } = default;

        public List<int> GanttChartShowConnections { get; init; } = [];



        public AllocationMode ResourceChartAllocationMode { get; init; } = default;

        public ScheduleMode ResourceChartScheduleMode { get; init; } = default;

        public DisplayStyle ResourceChartDisplayStyle { get; init; } = default;

        public bool ResourceChartShowToday { get; init; } = default;

        public bool ResourceChartShowMilestones { get; init; } = default;



        public bool EarnedValueShowProjections { get; init; } = default;

        public bool EarnedValueShowToday { get; init; } = default;

        public bool EarnedValueShowMilestones { get; init; } = default;
    }
}
