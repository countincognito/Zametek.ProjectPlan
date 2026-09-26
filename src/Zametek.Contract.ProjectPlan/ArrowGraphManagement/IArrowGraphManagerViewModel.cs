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

        // Writes the graph to the stream, laid out afresh by MSAGL rather than as arranged on screen, so that it needs no
        // interactive surface. The caller owns the stream, which is left open.
        Task WriteFixedLayoutArrowGraphImageAsync(Stream stream, GraphExportFormat format);
    }
}
