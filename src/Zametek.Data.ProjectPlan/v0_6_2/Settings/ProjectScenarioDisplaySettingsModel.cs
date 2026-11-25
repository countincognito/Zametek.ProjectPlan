using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.v0_6_2
{
    // v0.6.2 adds GanttChartShowHidden and ResourceChartShowHidden. The rest is the display settings of v0.6.1.
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



        public EdgeRoutingMode ArrowGraphEdgeRoutingMode { get; init; } = default;

        public EdgeRoutingMode VertexGraphEdgeRoutingMode { get; init; } = default;



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

        // The Gantt chart shows each activity, resource and work stream, also those that are hidden from the charts. It is
        // new in v0.6.2.
        public bool GanttChartShowHidden { get; init; } = default;



        public AllocationMode ResourceChartAllocationMode { get; init; } = default;

        public ScheduleMode ResourceChartScheduleMode { get; init; } = default;

        public DisplayStyle ResourceChartDisplayStyle { get; init; } = default;

        public bool ResourceChartShowToday { get; init; } = default;

        public bool ResourceChartShowMilestones { get; init; } = default;

        // The resource chart shows each activity, resource and work stream, also those that are hidden from the charts. It
        // is new in v0.6.2.
        public bool ResourceChartShowHidden { get; init; } = default;



        public bool EarnedValueShowProjections { get; init; } = default;

        public bool EarnedValueShowToday { get; init; } = default;

        public bool EarnedValueShowMilestones { get; init; } = default;

        public bool EarnedValueCombineResources { get; init; } = default;

        public bool EarnedValueScaleToOwnPlan { get; init; } = default;

        public List<int> EarnedValueShowResources { get; init; } = [];
    }
}
