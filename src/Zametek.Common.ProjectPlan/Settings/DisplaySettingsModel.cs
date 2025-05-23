﻿namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record DisplaySettingsModel
    {
        public bool ShowDates { get; init; }

        public bool UseClassicDates { get; init; }

        public bool UseBusinessDays { get; init; }



        public bool ArrowGraphShowNames { get; init; }



        public GroupByMode GanttChartGroupByMode { get; init; }

        public AnnotationStyle GanttChartAnnotationStyle { get; init; }

        public bool GanttChartShowGroupLabels { get; init; }

        public bool GanttChartShowProjectFinish { get; init; }

        public bool GanttChartShowTracking { get; init; }

        public bool GanttChartShowToday { get; init; }



        public AllocationMode ResourceChartAllocationMode { get; init; }

        public ScheduleMode ResourceChartScheduleMode { get; init; }

        public DisplayStyle ResourceChartDisplayStyle { get; init; }

        public bool ResourceChartShowToday { get; init; }



        public bool EarnedValueShowProjections { get; init; }

        public bool EarnedValueShowToday { get; init; }
    }
}
