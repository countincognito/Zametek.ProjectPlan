namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record DisplaySettingsModel
    {
        public bool ShowDates { get; init; } = default;

        public bool UseClassicDates { get; init; } = default;

        public bool UseBusinessDays { get; init; } = default;



        public bool ArrowGraphShowNames { get; init; } = default;



        public GroupByMode GanttChartGroupByMode { get; init; } = default;

        public AnnotationStyle GanttChartAnnotationStyle { get; init; } = default;

        public bool GanttChartShowGroupLabels { get; init; } = default;

        public bool GanttChartShowProjectFinish { get; init; } = default;

        public bool GanttChartShowTracking { get; init; } = default;

        public bool GanttChartShowToday { get; init; } = default;



        public AllocationMode ResourceChartAllocationMode { get; init; } = default;

        public ScheduleMode ResourceChartScheduleMode { get; init; } = default;

        public DisplayStyle ResourceChartDisplayStyle { get; init; } = default;

        public bool ResourceChartShowToday { get; init; } = default;



        public bool EarnedValueShowProjections { get; init; } = default;

        public bool EarnedValueShowToday { get; init; } = default;
    }
}
