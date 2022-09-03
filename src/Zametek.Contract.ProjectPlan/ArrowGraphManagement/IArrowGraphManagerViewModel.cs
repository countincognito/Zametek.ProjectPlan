using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IArrowGraphManagerViewModel
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        string ArrowGraphData { get; }

        ICommand SaveArrowGraphImageFileCommand { get; }
    }
}
