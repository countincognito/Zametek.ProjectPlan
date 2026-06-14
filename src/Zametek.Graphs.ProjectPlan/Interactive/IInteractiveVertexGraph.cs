using System.Collections.ObjectModel;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Graphs.ProjectPlan
{
    // The binding surface the InteractiveVertexGraphView control draws against. Implemented by the
    // host view-model (e.g. the application's VertexGraphManagerViewModel) so the control can be
    // embedded without the library depending on the application. Implementations are expected to
    // raise change notifications (INotifyPropertyChanged) for the bound properties.
    public interface IInteractiveVertexGraph
    {
        BaseTheme BaseTheme { get; }

        ObservableCollection<VertexGraphNodeViewModel> GraphNodes { get; }

        ObservableCollection<VertexGraphEdgeViewModel> GraphEdges { get; }

        double WorkspaceWidth { get; }

        double WorkspaceHeight { get; }

        ICommand SaveVertexGraphImageFileCommand { get; }

        void SelectNode(VertexGraphNodeViewModel? node);

        void OnNodeMoved(VertexGraphNodeViewModel node);

        void EnsureWorkspaceContains(VertexGraphNodeViewModel node);

        void ResetLayout();
    }
}
