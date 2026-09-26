using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceChartManagerViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        AllocationMode AllocationMode { get; set; }

        ScheduleMode ScheduleMode { get; set; }

        DisplayStyle DisplayStyle { get; set; }

        bool ShowToday { get; set; }

        bool ShowMilestones { get; set; }

        ICommand ResetResourceChartCommand { get; }

        ICommand SaveResourceChartImageFileCommand { get; }

        ICommand ChangeAllocationModeCommand { get; }

        ICommand ChangeScheduleModeCommand { get; }

        ICommand ChangeDisplayStyleCommand { get; }

        // Writes the chart to the stream at the given size. The caller owns the stream, which is left open.
        Task WriteResourceChartImageAsync(Stream stream, ChartImageFormat format, int width, int height);

        void BuildResourceChartPlotModel();
    }
}
