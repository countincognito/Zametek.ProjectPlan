using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Graphs.Avalonia
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

        // Persisted viewport transform (zoom + pan offset). The InteractiveGraphView writes these as
        // the user zooms/pans/fits, and reads them back when its control is rebuilt (e.g. after a dock
        // tab switch), so the framing survives the view being re-materialised. HasViewState is false
        // until the view has been framed at least once (before that the view does its initial auto-fit).
        double ViewZoom { get; set; }

        double ViewPanX { get; set; }

        double ViewPanY { get; set; }

        bool HasViewState { get; set; }

        // Raised by ResetView so an attached InteractiveGraphView drops its live zoom/pan and re-frames
        // the next graph from scratch.
        event EventHandler? ViewReset;

        // Raised after the graph is rebuilt or re-seeded, so the view re-frames a fresh load even when the
        // workspace size is unchanged (e.g. switching between scenarios with an identical graph).
        event EventHandler? GraphRefreshed;

        ICommand SaveGraphImageFileCommand { get; }

        // Set the edge routing mode. The menu's radio items bind to this, passing the chosen
        // GraphEdgeRoutingMode as the command parameter.
        ICommand ChangeEdgeRoutingModeCommand { get; }

        void SelectNode(GraphNodeViewModel? node);

        void OnNodeMoved(GraphNodeViewModel node);

        void EnsureWorkspaceContains(GraphNodeViewModel node);

        void ResetLayout();

        // Reset the viewport: zoom back to x1, pan to the origin, and clear the persisted framing
        // (ViewZoom/ViewPanX/ViewPanY/HasViewState) so the next graph is framed from scratch. Used when
        // the project scenario is reset or closed.
        void ResetView();

        // Rebuild the displayed graph from the host's current data and re-run the layout.
        void Refresh();

        // Export the graph to a file, choosing between the live interactive canvas and the fixed
        // MSAGL layout. GraphML/GraphViz exports are independent of the chosen source.
        Task SaveImageAsync(string? filename, GraphImageSource source, FixedLayoutGraphType imageType);
    }
}
