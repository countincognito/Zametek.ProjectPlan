using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IArrowGraphManagerViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        string ArrowGraphData { get; }

        BaseTheme BaseTheme { get; }

        ICommand SaveArrowGraphImageFileCommand { get; }

        Task SaveArrowGraphImageFileAsync(string? filename);

        void BuildArrowGraphDiagramData();

        void BuildArrowGraphDiagramImage();
    }
}
