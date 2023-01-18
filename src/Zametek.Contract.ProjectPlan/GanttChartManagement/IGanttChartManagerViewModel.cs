using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IGanttChartManagerViewModel
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        ICommand SaveGanttChartImageFileCommand { get; }
    }
}
