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

        // Writes the graph to the stream, laid out afresh by MSAGL rather than as arranged on screen, so that it needs no
        // interactive surface. The caller owns the stream, which is left open.
        Task WriteFixedLayoutVertexGraphImageAsync(Stream stream, GraphExportFormat format);
    }
}
