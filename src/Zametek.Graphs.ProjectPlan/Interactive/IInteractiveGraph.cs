using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Graphs.ProjectPlan
{
    // The contract the InteractiveGraphView control draws against, and the operations used to drive
    // the interactive graph. Implemented by InteractiveGraphViewModel (the reusable, self-contained
    // implementation in this library), which serves both the arrow and vertex graphs. Implementations
    // are expected to raise change notifications (INotifyPropertyChanged) for the bound properties.
    public interface IInteractiveGraph
    {
        GraphTheme Theme { get; }

        // Whether this graph offers the "show names" toggle (the view hides the menu item when false).
        bool SupportsShowNames { get; }

        bool ShowNames { get; set; }

        ObservableCollection<GraphNodeViewModel> GraphNodes { get; }

        ObservableCollection<GraphEdgeViewModel> GraphEdges { get; }

        double WorkspaceWidth { get; }

        double WorkspaceHeight { get; }

        ICommand SaveGraphImageFileCommand { get; }

        void SelectNode(GraphNodeViewModel? node);

        void OnNodeMoved(GraphNodeViewModel node);

        void EnsureWorkspaceContains(GraphNodeViewModel node);

        void ResetLayout();

        // Rebuild the displayed graph from the host's current data and re-run the layout.
        void Refresh();

        // Export the graph to a file, choosing between the live interactive canvas and the fixed
        // MSAGL layout. GraphML/GraphViz exports are independent of the chosen source.
        Task SaveImageAsync(string? filename, GraphImageSource source);
    }
}
