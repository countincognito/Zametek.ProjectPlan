using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IGanttChartManagerViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        GroupByMode GroupByMode { get; set; }

        AnnotationStyle AnnotationStyle { get; set; }

        bool ShowGroupLabels { get; set; }

        bool ShowProjectFinish { get; set; }

        bool ShowTracking { get; set; }

        bool ShowToday { get; set; }

        bool ShowMilestones { get; set; }

        bool ShowSlack { get; set; }

        bool ShowNonWorkingDays { get; set; }

        bool ShowDates { get; }

        DateTimeOffset ProjectStart { get; }

        IActivitySelectorViewModel ActivitySelector { get; }

        ICommand ResetGanttChartCommand { get; }

        ICommand SaveGanttChartImageFileCommand { get; }

        ICommand ChangeGroupByModeCommand { get; }

        ICommand ChangeAnnotationStyleCommand { get; }

        // Writes the chart to the stream at the given width, and at the given height or taller: the chart grows to give
        // every bar a readable height. The caller owns the stream, which is left open.
        Task WriteGanttChartImageAsync(Stream stream, ChartImageFormat format, int width, int height);

        void SetActivityDuration(int activityId, int newDuration);

        void BuildGanttChartPlotModel();
    }
}
