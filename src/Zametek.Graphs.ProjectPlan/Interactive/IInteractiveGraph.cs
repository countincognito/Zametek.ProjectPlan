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

        // The current edge routing strategy (it shapes the interactive edges and drives the
        // layout/export). The context menu's radio items read this to show the current selection and
        // set it through ChangeEdgeRoutingModeCommand.
        GraphEdgeRoutingMode EdgeRoutingMode { get; }

        ObservableCollection<GraphNodeViewModel> GraphNodes { get; }

        ObservableCollection<GraphEdgeViewModel> GraphEdges { get; }

        double WorkspaceWidth { get; }

        double WorkspaceHeight { get; }

        ICommand SaveGraphImageFileCommand { get; }

        // Set the edge routing mode. The menu's radio items bind to this, passing the chosen
        // GraphEdgeRoutingMode as the command parameter.
        ICommand ChangeEdgeRoutingModeCommand { get; }

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
