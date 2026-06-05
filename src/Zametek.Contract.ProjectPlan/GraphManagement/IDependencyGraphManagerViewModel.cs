using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IDependencyGraphManagerViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        string DependencyGraphData { get; }

        BaseTheme BaseTheme { get; }

        ICommand SaveDependencyGraphImageFileCommand { get; }

        Task SaveDependencyGraphImageFileAsync(string? filename);

        void BuildDependencyGraphDiagramData();

        void BuildDependencyGraphDiagramImage();
    }
}
