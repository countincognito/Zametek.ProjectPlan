using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IScenarioChartManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        TrackedMetrics TrackedMetricXAxis { get; set; }

        TrackedMetrics TrackedMetricYAxis { get; set; }

        //ScheduleMode ScheduleMode { get; set; }

        //DisplayStyle DisplayStyle { get; set; }

        //bool ShowToday { get; set; }

        ICommand SaveScenarioChartImageFileCommand { get; }

        Task SaveScenarioChartImageFileAsync(string? filename, int width, int height);

        void BuildScenarioChartPlotModel();
    }
}
