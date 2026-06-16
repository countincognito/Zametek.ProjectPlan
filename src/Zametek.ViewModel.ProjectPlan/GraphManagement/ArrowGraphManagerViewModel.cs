using Avalonia.Svg.Skia;
using Avalonia.Threading;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Graphs.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    // Application glue for the arrow graph. The interactive viewer itself now lives in the reusable
    // InteractiveArrowGraphViewModel (in Zametek.Graphs.ProjectPlan); this view-model supplies that
    // control with the application's data and dialogs via IArrowGraphHost, keeps the headless
    // SVG export members the CLI calls, and exposes the interactive view-model to the embedded view.
    public class ArrowGraphManagerViewModel
        : ToolViewModelBase, IArrowGraphManagerViewModel, IArrowGraphHost
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
        private readonly IArrowGraphSerializer m_ArrowGraphExport;
        private readonly IGraphImageExporter m_GraphImageExporter;

        // The reusable, self-contained interactive arrow graph. It owns all of the
        // node/edge/workspace/drag/select/layout/export behaviour and subscribes to RebuildRequested.
        private readonly InteractiveArrowGraphViewModel m_Interactive;

        #endregion

        #region Ctors

        public ArrowGraphManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService,
            IArrowGraphSerializer arrowGraphExport,
            IGraphImageExporter graphImageExporter)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(arrowGraphExport);
            ArgumentNullException.ThrowIfNull(graphImageExporter);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_ArrowGraphExport = arrowGraphExport;
            m_GraphImageExporter = graphImageExporter;

            m_ArrowGraphData = string.Empty;
            m_ArrowGraphImage = new SvgImage();

            m_IsBusy = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.IsBusy)
                .ToProperty(this, agm => agm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, agm => agm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, agm => agm.HasCompilationErrors);

            m_ShowNames = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames)
                .ToProperty(this, agm => agm.ShowNames);

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
                    agm => agm.m_CoreViewModel.ArrowGraph,
                    agm => agm.m_CoreViewModel.GraphSettings,
                    agm => agm.m_CoreViewModel.BaseTheme,
                    agm => agm.m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames)
                .MuteWhile(this.WhenAnyValue(agm => agm.m_CoreViewModel.IsBulkUpdating))
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(_ => Unit.Default);

            m_Interactive = new InteractiveArrowGraphViewModel(this, m_ArrowGraphExport, m_GraphImageExporter);

            Id = Resource.ProjectPlan.Titles.Title_ArrowGraphView;
            Title = Resource.ProjectPlan.Titles.Title_ArrowGraphView;
        }

        #endregion

        #region Properties

        private SvgImage m_ArrowGraphImage;
        public SvgImage ArrowGraphImage
        {
            get => m_ArrowGraphImage;
            private set
            {
                this.RaiseAndSetIfChanged(ref m_ArrowGraphImage, value);
            }
        }

        // The reusable interactive viewer the embedded InteractiveArrowGraphView binds to.
        public IInteractiveArrowGraph Interactive => m_Interactive;

        #endregion

        #region IArrowGraphHost Members

        private readonly ObservableAsPropertyHelper<GraphTheme> m_Theme;
        public GraphTheme Theme => m_Theme.Value;

        // Build the library-neutral diagram (what to draw) from the application's domain graph (with
        // presentation resolved). The interactive/SVG paths use single-line edge labels; the
        // GraphML/GraphViz exports use multi-line labels. Locked so it serialises with the headless
        // SVG build below.
        public DiagramGraphModel BuildDiagram(bool multiLineEdgeLabels)
        {
            lock (m_Lock)
            {
                return BuildArrowDiagram(multiLineEdgeLabels);
            }
        }

        private readonly IObservable<Unit> m_RebuildRequested;
        public IObservable<Unit> RebuildRequested => m_RebuildRequested;

        public async Task<string?> PickSaveFileAsync()
        {
            string title = m_SettingService.ProjectTitle;
            title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
            string graphOutputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_ArrowChart}";
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
        private DiagramGraphModel BuildArrowDiagram(bool multiLineEdgeLabels)
        {
            return ArrowGraphDiagramBuilder.Build(
                GraphPresentationBuilder.ApplyPresentation(m_CoreViewModel.ArrowGraph, m_CoreViewModel.GraphSettings),
                multiLineEdgeLabels,
                m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames);
        }

        #endregion

        #region IArrowGraphManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ShowNames;
        public bool ShowNames
        {
            get => m_ShowNames.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames = value;
            }
        }

        private string m_ArrowGraphData;
        public string ArrowGraphData
        {
            get => m_ArrowGraphData;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ArrowGraphData, value);
                }
            }
        }

        private readonly ObservableAsPropertyHelper<BaseTheme> m_BaseTheme;
        public BaseTheme BaseTheme => m_BaseTheme.Value;

        // Delegates to the interactive viewer's Save-As (which prompts and renders the live canvas).
        public ICommand SaveArrowGraphImageFileCommand => m_Interactive.SaveArrowGraphImageFileCommand;

        // Export to a specific file. Used by the headless CLI, so it exports the fixed MSAGL layout
        // (which needs no populated interactive surface) rather than the on-screen canvas.
        public Task SaveArrowGraphImageFileAsync(string? filename)
        {
            return m_Interactive.SaveImageAsync(filename, ArrowGraphImageSource.FixedLayout);
        }

        public void BuildArrowGraphDiagramData()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(ArrowGraphManagerViewModel)}.{nameof(BuildArrowGraphDiagramData)}");
            byte[]? data = null;

            lock (m_Lock)
            {
                if (!HasCompilationErrors)
                {
                    data = m_ArrowGraphExport.BuildArrowGraphSvgData(
                        BuildArrowDiagram(multiLineEdgeLabels: false),
                        m_CoreViewModel.BaseTheme.ToGraphTheme());
                }
            }

            ArrowGraphData = data?.ByteArrayToString() ?? string.Empty;
        }

        public void BuildArrowGraphDiagramImage()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(ArrowGraphManagerViewModel)}.{nameof(BuildArrowGraphDiagramImage)}");
            SvgSource? source = null;

            lock (m_Lock)
            {
                string arrowGraphData = ArrowGraphData;
                if (!string.IsNullOrWhiteSpace(arrowGraphData))
                {
                    source = SvgSource.LoadFromSvg(arrowGraphData);
                }
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                var image = new SvgImage
                {
                    Source = source
                };
                ArrowGraphImage = image;
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
                m_ShowNames?.Dispose();
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
