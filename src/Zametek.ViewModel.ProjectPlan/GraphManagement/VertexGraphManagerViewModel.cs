using Avalonia.Svg.Skia;
using Avalonia.Threading;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Graphs.Avalonia;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    // Application glue for the vertex graph. The interactive viewer itself now lives in the reusable
    // InteractiveGraphViewModel (in Zametek.Graphs.Avalonia); this view-model supplies that
    // control with the application's data and dialogs via IGraphHost, keeps the headless SVG export
    // members the CLI calls, and exposes the interactive view-model to the embedded view. The graph's
    // per-type differences come from GraphConfigurations.Vertex (which, unlike the arrow graph, does
    // not surface a show-names toggle).
    public class VertexGraphManagerViewModel
        : ToolViewModelBase, IVertexGraphManagerViewModel, IGraphHost
    {
        #region Fields

        private readonly Lock m_Lock;

        private static readonly IList<IFileFilter> s_ExportFileFilters =
            [
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImageJpegFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ImageJpegFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImagePngFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ImagePngFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_PdfFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_PdfFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImageSvgFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ImageSvgFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_GraphMLFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_GraphMLFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_GraphVizFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_GraphVizFilePattern
                    ]
                }
            ];

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IGraphLayoutEngine m_LayoutEngine;

        // The reusable, self-contained interactive graph. It owns all of the
        // node/edge/workspace/drag/select/layout/export behaviour and subscribes to RebuildRequested.
        private readonly InteractiveGraphViewModel m_Interactive;

        #endregion

        #region Ctors

        public VertexGraphManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService,
            IGraphLayoutEngine layoutEngine)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(layoutEngine);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_LayoutEngine = layoutEngine;

            m_VertexGraphData = string.Empty;
            m_VertexGraphImage = new SvgImage();

            m_IsBusy = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.IsBusy)
                .ToProperty(this, agm => agm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, agm => agm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, agm => agm.HasCompilationErrors);

            m_BaseTheme = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.BaseTheme)
                .ToProperty(this, agm => agm.BaseTheme);

            // The interactive control binds to the library's own GraphTheme (mapped from BaseTheme).
            m_Theme = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.BaseTheme)
                .Select(x => x.ToGraphTheme())
                .ToProperty(this, agm => agm.Theme);

            // The single live rebuild trigger: the domain graph, the graph settings, the theme or the
            // show-names setting changing. Conflated while a project scenario is loaded/reset, and
            // delivered off the UI thread. The interactive view-model subscribes to this and runs the
            // MSAGL layout once per change (the headless SVG is built lazily, only when exporting).
            m_RebuildRequested = this
                .WhenAnyValue(
                    agm => agm.m_CoreViewModel.VertexGraph,
                    agm => agm.m_CoreViewModel.GraphSettings,
                    agm => agm.m_CoreViewModel.BaseTheme,
                    agm => agm.m_CoreViewModel.DisplaySettingsViewModel.VertexGraphShowNames)
                .MuteWhile(this.WhenAnyValue(agm => agm.m_CoreViewModel.IsBulkUpdating))
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(_ => Unit.Default);

            m_Interactive = new InteractiveGraphViewModel(this, m_LayoutEngine, new GraphSerializer(), GraphConfigurations.Vertex);

            Id = Resource.ProjectPlan.Titles.Title_VertexGraphView;
            Title = Resource.ProjectPlan.Titles.Title_VertexGraphView;
        }

        #endregion

        #region Properties

        private SvgImage m_VertexGraphImage;
        public SvgImage VertexGraphImage
        {
            get => m_VertexGraphImage;
            private set
            {
                this.RaiseAndSetIfChanged(ref m_VertexGraphImage, value);
            }
        }

        // The reusable interactive viewer the embedded InteractiveGraphView binds to.
        public IInteractiveGraph Interactive => m_Interactive;

        #endregion

        #region IGraphHost Members

        private readonly ObservableAsPropertyHelper<GraphTheme> m_Theme;
        public GraphTheme Theme => m_Theme.Value;

        // The vertex graph does not surface a show-names toggle (GraphConfigurations.Vertex sets
        // SupportsShowNames = false), but the host contract still carries it; it is backed by the
        // persisted setting and is simply never displayed.
        public bool ShowNames
        {
            get => m_CoreViewModel.DisplaySettingsViewModel.VertexGraphShowNames;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.VertexGraphShowNames = value;
            }
        }

        // Build the library-neutral diagram (what to draw) from the application's domain graph (with
        // presentation resolved). The vertex graph has no edge labels, so multiLineEdgeLabels is
        // ignored. Locked so it serialises with the headless SVG build below.
        public DiagramGraphModel BuildDiagram(bool multiLineEdgeLabels)
        {
            lock (m_Lock)
            {
                return BuildVertexDiagram();
            }
        }

        private readonly IObservable<Unit> m_RebuildRequested;
        public IObservable<Unit> RebuildRequested => m_RebuildRequested;

        public async Task<string?> PickSaveFileAsync()
        {
            string title = m_SettingService.ProjectTitle;
            title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
            string graphOutputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_VertexChart}";
            string directory = m_SettingService.ProjectDirectory;
            return await m_DialogService.ShowSaveFileDialogAsync(graphOutputFile, directory, s_ExportFileFilters);
        }

        public Task ReportErrorAsync(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            return m_DialogService.ShowErrorAsync(
                Resource.ProjectPlan.Titles.Title_Error,
                string.Empty,
                exception.Message);
        }

        #endregion

        #region Private Methods

        // Map the application's domain graph (with presentation resolved) into the library-neutral
        // DiagramGraphModel the serializer consumes.
        private DiagramGraphModel BuildVertexDiagram()
        {
            return VertexGraphDiagramBuilder.Build(
                GraphPresentationBuilder.ApplyPresentation(m_CoreViewModel.VertexGraph, m_CoreViewModel.GraphSettings));
        }

        #endregion

        #region IVertexGraphManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private string m_VertexGraphData;
        public string VertexGraphData
        {
            get => m_VertexGraphData;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_VertexGraphData, value);
                }
            }
        }

        private readonly ObservableAsPropertyHelper<BaseTheme> m_BaseTheme;
        public BaseTheme BaseTheme => m_BaseTheme.Value;

        // Delegates to the interactive viewer's Save-As (which prompts and renders the live canvas).
        public ICommand SaveVertexGraphImageFileCommand => m_Interactive.SaveGraphImageFileCommand;

        // Export to a specific file. Used by the headless CLI, so it exports the fixed MSAGL layout
        // (which needs no populated interactive surface) rather than the on-screen canvas.
        public Task SaveVertexGraphImageFileAsync(string? filename)
        {
            return m_Interactive.SaveImageAsync(filename, GraphImageSource.FixedLayout);
        }

        public void BuildVertexGraphDiagramData()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(VertexGraphManagerViewModel)}.{nameof(BuildVertexGraphDiagramData)}");
            byte[]? data = null;

            lock (m_Lock)
            {
                if (!HasCompilationErrors)
                {
                    data = m_LayoutEngine.RenderSvg(
                        BuildVertexDiagram(),
                        m_Interactive.Configuration,
                        m_CoreViewModel.BaseTheme.ToGraphTheme());
                }
            }

            VertexGraphData = data?.ByteArrayToString() ?? string.Empty;
        }

        public void BuildVertexGraphDiagramImage()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(VertexGraphManagerViewModel)}.{nameof(BuildVertexGraphDiagramImage)}");
            SvgSource? source = null;

            lock (m_Lock)
            {
                string vertexGraphData = VertexGraphData;
                if (!string.IsNullOrWhiteSpace(vertexGraphData))
                {
                    source = SvgSource.LoadFromSvg(vertexGraphData);
                }
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                var image = new SvgImage
                {
                    Source = source
                };
                VertexGraphImage = image;
            });
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_Interactive.Dispose();
        }

        #endregion

        #region IDisposable Members

        private bool m_Disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                KillSubscriptions();
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_BaseTheme?.Dispose();
                m_Theme?.Dispose();
            }

            m_Disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
