using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Graphs.ProjectPlan
{
    // The contract the InteractiveVertexGraphView control draws against, and the operations used to
    // drive the interactive graph. Implemented by InteractiveVertexGraphViewModel (the reusable,
    // self-contained implementation in this library). Implementations are expected to raise change
    // notifications (INotifyPropertyChanged) for the bound properties.
    public interface IInteractiveVertexGraph
    {
        GraphTheme Theme { get; }

        ObservableCollection<VertexGraphNodeViewModel> GraphNodes { get; }

        ObservableCollection<VertexGraphEdgeViewModel> GraphEdges { get; }

        double WorkspaceWidth { get; }

        double WorkspaceHeight { get; }

        ICommand SaveVertexGraphImageFileCommand { get; }

        void SelectNode(VertexGraphNodeViewModel? node);

        void OnNodeMoved(VertexGraphNodeViewModel node);

        void EnsureWorkspaceContains(VertexGraphNodeViewModel node);

        void ResetLayout();

        // Rebuild the displayed graph from the host's current data and re-run the layout.
        void Refresh();

        // Export the graph to a file, choosing between the live interactive canvas and the fixed
        // MSAGL layout. GraphML/GraphViz exports are independent of the chosen source.
        Task SaveImageAsync(string? filename, VertexGraphImageSource source);
    }
}
