using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IDisplaySettingsViewModel
        : IDisposable
    {
        bool ShowDates { get; set; }
        bool UseClassicDates { get; set; }
        bool UseBusinessDays { get; set; }
        bool HideCost { get; set; }
        bool HideBilling { get; set; }


        bool ArrowGraphShowNames { get; set; }


        GroupByMode GanttChartGroupByMode { get; set; }
        AnnotationStyle GanttChartAnnotationStyle { get; set; }
        bool GanttChartShowGroupLabels { get; set; }
        bool GanttChartShowProjectFinish { get; set; }
        bool GanttChartShowTracking { get; set; }
        bool GanttChartShowToday { get; set; }


        AllocationMode ResourceChartAllocationMode { get; set; }
        ScheduleMode ResourceChartScheduleMode { get; set; }
        DisplayStyle ResourceChartDisplayStyle { get; set; }
        bool ResourceChartShowToday { get; set; }


        public bool EarnedValueShowProjections { get; set; }
        public bool EarnedValueShowToday { get; set; }


        public void SetValues(DisplaySettingsModel model);
        public DisplaySettingsModel GetValues();
    }
}
