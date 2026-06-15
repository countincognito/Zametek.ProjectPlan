using System.Collections.ObjectModel;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Graphs.ProjectPlan
{
    // The binding surface the InteractiveArrowGraphView control draws against. Implemented by the
    // host view-model (e.g. the application's ArrowGraphManagerViewModel) so the control can be
    // embedded without the library depending on the application. Implementations are expected to
    // raise change notifications (INotifyPropertyChanged) for the bound properties.
    public interface IInteractiveArrowGraph
    {
        BaseTheme BaseTheme { get; }

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
    }
}
