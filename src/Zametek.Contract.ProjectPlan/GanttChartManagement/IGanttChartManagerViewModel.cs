using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IGanttChartManagerViewModel
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool GroupByResource { get; set; }

        bool AnnotateResources { get; set; }

        ICommand SaveGanttChartImageFileCommand { get; }
    }
}
