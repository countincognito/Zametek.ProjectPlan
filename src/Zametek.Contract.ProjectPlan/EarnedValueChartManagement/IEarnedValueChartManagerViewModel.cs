using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IEarnedValueChartManagerViewModel
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        ICommand SaveEarnedValueChartImageFileCommand { get; }
    }
}
