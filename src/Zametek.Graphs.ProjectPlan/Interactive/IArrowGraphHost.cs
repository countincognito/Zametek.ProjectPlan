using System.Reactive;

namespace Zametek.Graphs.ProjectPlan
{
    // The thin set of application services InteractiveArrowGraphViewModel needs so it can source its
    // data and complete a Save-As without the control library depending on the application. The host
    // (e.g. the application's ArrowGraphManagerViewModel) owns the domain graph, the persisted
    // settings and the dialogs; the view-model owns all of the interactive/layout/export behaviour.
    public interface IArrowGraphHost
    {
        // The current theme the graph is drawn against.
        GraphTheme Theme { get; }

        // Whether activity names are shown on the edges. Setting it persists the host's setting; the
        // host is expected to raise a RebuildRequested notification in response.
        bool ShowNames { get; set; }

        // True when the project does not currently compile, so there is nothing to draw.
        bool HasCompilationErrors { get; }

        // Build the library-neutral diagram to draw/export from the host's current domain graph.
        // multiLineEdgeLabels distinguishes the interactive/SVG path (false) from the
        // GraphML/GraphViz path (true).
        DiagramGraphModel BuildDiagram(bool multiLineEdgeLabels);

        // Fires when the displayed graph should be rebuilt (the domain graph, settings or theme
        // changed). The host is responsible for any throttling/scheduling; the view-model simply
        // rebuilds on each notification.
        IObservable<Unit> RebuildRequested { get; }

        // Prompt the user for a save path (returns null if cancelled).
        Task<string?> PickSaveFileAsync();

        // Surface an error to the user.
        Task ReportErrorAsync(Exception exception);
    }
}
