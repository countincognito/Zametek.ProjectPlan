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

        Task SaveGanttChartImageFileAsync(string? filename, int width, int height);

        Task<byte[]?> RenderGanttChartImageAsync();

        void SetActivityDuration(int activityId, int newDuration);

        void BuildGanttChartPlotModel();
    }
}
