using System;
using Zametek.Graphs.Avalonia;
using Zametek.Graphs.Avalonia.TestApp.Graphs;

namespace Zametek.Graphs.Avalonia.TestApp.ViewModels
{
    // Base for one demo tab. It owns the DemoGraphHost and the library's reusable
    // InteractiveGraphViewModel, and exposes the latter as Interactive for the embedded
    // InteractiveGraphView to draw against. Subclasses only choose the title, the diagram, the
    // GraphConfiguration preset and (optionally) a bespoke GraphAppearance - all the interactive
    // behaviour comes from the library.
    public abstract class GraphTabViewModelBase
        : ViewModelBase, IDisposable
    {
        private readonly DemoGraphHost m_Host;
        private readonly InteractiveGraphViewModel m_Interactive;
        private bool m_Disposed;

        protected GraphTabViewModelBase(
            string title,
            Func<bool, DiagramGraphModel> buildDiagram,
            GraphConfiguration configuration,
            GraphAppearance? appearance,
            GraphTheme initialTheme,
            string suggestedFileName)
        {
            Title = title;
            m_Host = new DemoGraphHost(buildDiagram, initialTheme, suggestedFileName);

            // The one wiring line a consumer writes: hand the host, a layout engine, a serializer, the
            // per-graph configuration preset and (optionally) a re-skinning appearance to the reusable
            // view-model. Passing null for the appearance keeps the library's default look.
            m_Interactive = new InteractiveGraphViewModel(
                m_Host,
                new MsaglGraphLayoutEngine(),
                new GraphSerializer(),
                configuration,
                appearance);
        }

        public string Title { get; }

        // The IInteractiveGraph the embedded InteractiveGraphView binds to (drag, select, zoom/pan,
        // copy/save all come from here).
        public IInteractiveGraph Interactive => m_Interactive;

        // Push a new theme into this tab's graph (the main window's toggle calls this for every tab).
        public void ApplyTheme(GraphTheme theme) => m_Host.SetTheme(theme);

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }
            m_Disposed = true;
            m_Interactive.Dispose();
            m_Host.Dispose();
        }
    }
}
