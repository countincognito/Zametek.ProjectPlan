using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Graphs.ProjectPlan
{
    // The contract the InteractiveArrowGraphView control draws against, and the operations used to
    // drive the interactive graph. Implemented by InteractiveArrowGraphViewModel (the reusable,
    // self-contained implementation in this library). Implementations are expected to raise change
    // notifications (INotifyPropertyChanged) for the bound properties.
    public interface IInteractiveArrowGraph
    {
        GraphTheme Theme { get; }

        bool ShowNames { get; set; }

        ObservableCollection<ArrowGraphNodeViewModel> GraphNodes { get; }

        ObservableCollection<ArrowGraphEdgeViewModel> GraphEdges { get; }

        double WorkspaceWidth { get; }

        double WorkspaceHeight { get; }

        ICommand SaveArrowGraphImageFileCommand { get; }

        void SelectNode(ArrowGraphNodeViewModel? node);

        void OnNodeMoved(ArrowGraphNodeViewModel node);

        void EnsureWorkspaceContains(ArrowGraphNodeViewModel node);

        void ResetLayout();

        // Rebuild the displayed graph from the host's current data and re-run the layout.
        void Refresh();

        // Export the graph to a file, choosing between the live interactive canvas and the fixed
        // MSAGL layout. GraphML/GraphViz exports are independent of the chosen source.
        Task SaveImageAsync(string? filename, ArrowGraphImageSource source);
    }
}
