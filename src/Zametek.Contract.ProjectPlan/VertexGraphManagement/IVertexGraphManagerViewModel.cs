using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IVertexGraphManagerViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        BaseTheme BaseTheme { get; }

        ICommand SaveVertexGraphImageFileCommand { get; }

        Task SaveFixedLayoutVertexGraphImageFileAsync(string? filename);
    }
}
