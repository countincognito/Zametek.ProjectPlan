using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IGanttChartManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        GroupByMode GroupByMode { get; set; }

        bool GroupBy { get; }

        bool AnnotateGroups { get; set; }

        ICommand SaveGanttChartImageFileCommand { get; }

        Task SaveGanttChartImageFileAsync(string? filename, int width, int height);

        void BuildGanttChartPlotModel();
    }
}
