using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Zametek.Graphs.Avalonia;

namespace Zametek.Graphs.Avalonia.TestApp.Graphs
{
    // A compact IGraphHost for the demo. In a real application this role is played by something like the
    // ProjectPlan ArrowGraphManagerViewModel: it owns the domain graph, the theme, and the dialogs, and
    // feeds the reusable InteractiveGraphViewModel through this thin contract. Here the "domain graph" is
    // just a SampleGraphs factory, the theme is a field the toolbar flips, and the dialogs are the small
    // DemoDialogs helpers. Everything interactive (drag, select, zoom/pan, layout, copy/save) is owned by
    // the library, not by this host.
    internal sealed class DemoGraphHost
        : IGraphHost, IDisposable
    {
        private readonly Func<bool, DiagramGraphModel> m_BuildDiagram;
        private readonly string m_SuggestedFileName;

        // Seeded with a value so subscribing (the view-model does so in its constructor) produces the
        // initial layout; pushed again whenever the theme or the show-names toggle changes.
        private readonly BehaviorSubject<Unit> m_Rebuild = new(Unit.Default);

        private GraphTheme m_Theme;
        private bool m_ShowNames;
        private bool m_Disposed;

        public DemoGraphHost(
            Func<bool, DiagramGraphModel> buildDiagram,
            GraphTheme initialTheme,
            string suggestedFileName)
        {
            ArgumentNullException.ThrowIfNull(buildDiagram);
            m_BuildDiagram = buildDiagram;
            m_Theme = initialTheme;
            m_SuggestedFileName = suggestedFileName;
        }

        public GraphTheme Theme => m_Theme;

        public bool ShowNames
        {
            get => m_ShowNames;
            set
            {
                if (m_ShowNames == value)
                {
                    return;
                }
                m_ShowNames = value;
                RequestRebuild();
            }
        }

        // The demo graph always compiles - there is nothing to fail.
        public bool HasCompilationErrors => false;

        // Produce the neutral diagram to draw. The multiLineEdgeLabels flag distinguishes the
        // interactive/SVG path from the GraphML/GraphViz path; the sample graphs do not vary by it, so it
        // is ignored here (the show-names toggle is what actually changes the labels).
        public DiagramGraphModel BuildDiagram(bool multiLineEdgeLabels) => m_BuildDiagram(m_ShowNames);

        // Fires once on subscription (the BehaviorSubject seed) for the initial layout, then on each
        // theme / show-names change. Observed on the task pool so the MSAGL layout the view-model runs in
        // response never blocks the UI thread - the same scheduling a real host applies.
        public IObservable<Unit> RebuildRequested => m_Rebuild.ObserveOn(TaskPoolScheduler.Default);

        public Task<string?> PickSaveFileAsync() => DemoDialogs.PickSaveFileAsync(m_SuggestedFileName);

        public Task ReportErrorAsync(string message) => DemoDialogs.ShowErrorAsync(message);

        // Change the theme and rebuild so the canvas background (and the exported image) follow. The
        // view-model re-raises Theme as part of the rebuild, so the bound background updates.
        public void SetTheme(GraphTheme theme)
        {
            if (m_Theme == theme)
            {
                return;
            }
            m_Theme = theme;
            RequestRebuild();
        }

        private void RequestRebuild()
        {
            if (!m_Disposed)
            {
                m_Rebuild.OnNext(Unit.Default);
            }
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }
            m_Disposed = true;
            m_Rebuild.OnCompleted();
            m_Rebuild.Dispose();
        }
    }
}
