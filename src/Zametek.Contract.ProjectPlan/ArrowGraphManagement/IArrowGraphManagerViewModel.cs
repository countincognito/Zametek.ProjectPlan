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

        bool ShowNames { get; set; }

        BaseTheme BaseTheme { get; }

        ICommand SaveArrowGraphImageFileCommand { get; }

        Task SaveFixedLayoutArrowGraphImageFileAsync(string? filename);
    }
}
