using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectScenarioDisplaySettingsViewModel
        : IDisposable
    {
        bool ShowDates { get; set; }
        bool UseClassicDates { get; set; }
        NonWorkingDayMode NonWorkingDayMode { get; set; }
        bool HideCost { get; set; }
        bool HideBilling { get; set; }


        bool ArrowGraphShowNames { get; set; }


        bool VertexGraphShowNames { get; set; }


        EdgeRoutingMode ArrowGraphEdgeRoutingMode { get; set; }


        EdgeRoutingMode VertexGraphEdgeRoutingMode { get; set; }


        GroupByMode GanttChartGroupByMode { get; set; }
        AnnotationStyle GanttChartAnnotationStyle { get; set; }
        bool GanttChartShowGroupLabels { get; set; }
        bool GanttChartShowProjectFinish { get; set; }
        bool GanttChartShowTracking { get; set; }
        bool GanttChartShowToday { get; set; }
        bool GanttChartShowMilestones { get; set; }
        bool GanttChartShowSlack { get; set; }
        bool GanttChartShowNonWorkingDays { get; set; }

        /// <summary>
        /// The filter of connections. It is replaced as a whole and it never changes in place, and so a reader on any thread
        /// needs no lock, and it can keep what it has read.
        /// </summary>
        IReadOnlyList<int> GanttChartShowConnections { get; }

        /// <summary>
        /// Replaces the filter of connections, for an edit by the user. It raises nothing, because the edit comes from the
        /// selector that follows the filter.
        /// </summary>
        void SetGanttChartShowConnections(IEnumerable<int> connections);

        /// <summary>
        /// How many times SetValues has put a filter in GanttChartShowConnections. An edit by the user does not count, because
        /// it comes from the selector that follows this. Observe changes only.
        /// </summary>
        int GanttChartShowConnectionsRevision { get; }


        AllocationMode ResourceChartAllocationMode { get; set; }
        ScheduleMode ResourceChartScheduleMode { get; set; }
        DisplayStyle ResourceChartDisplayStyle { get; set; }
        bool ResourceChartShowToday { get; set; }
        bool ResourceChartShowMilestones { get; set; }


        bool EarnedValueShowProjections { get; set; }
        bool EarnedValueShowToday { get; set; }
        bool EarnedValueShowMilestones { get; set; }
        bool EarnedValueCombineResources { get; set; }
        bool EarnedValueScaleToOwnPlan { get; set; }

        /// <summary>
        /// The filter of resources. It is replaced as a whole and it never changes in place, and so a reader on any thread
        /// needs no lock, and it can keep what it has read.
        /// </summary>
        IReadOnlyList<int> EarnedValueShowResources { get; }

        /// <summary>
        /// Replaces the filter of resources, for an edit by the user. It raises nothing, because the edit comes from the
        /// selector that follows the filter.
        /// </summary>
        void SetEarnedValueShowResources(IEnumerable<int> resources);

        /// <summary>
        /// How many times SetValues has put a filter in EarnedValueShowResources. An edit by the user does not count, because
        /// it comes from the selector that follows this. Observe changes only.
        /// </summary>
        int EarnedValueShowResourcesRevision { get; }


        void SetIsProjectScenarioUpdated(bool isProjectScenarioUpdated);

        void SetValues(ProjectScenarioDisplaySettingsModel model);
        ProjectScenarioDisplaySettingsModel GetValues();
    }
}
