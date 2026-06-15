using Avalonia.Svg.Skia;
using Avalonia.Threading;
using ReactiveUI;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Graphs.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ArrowGraphManagerViewModel
        : ToolViewModelBase, IArrowGraphManagerViewModel, IInteractiveArrowGraph
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

        private readonly IDisposable? m_BuildArrowGraphInteractiveSub;

        // Interactive arrow-graph state.
        private Dictionary<int, HashSet<int>> m_Adjacency = [];
        private ArrowGraphNodeViewModel? m_SelectedNode;

        // Positions of nodes the user has dragged, preserved across re-layouts.
        private readonly Dictionary<int, (double X, double Y)> m_ManualNodePositions = [];

        // The interactive surface (graphCanvas / ItemsControls) is sized to the workspace, not the
        // graph: a fixed margin is added on every side and the workspace grows as nodes are dragged
        // outward, so a dragged node always stays inside the arrange bounds and never gets clipped
        // away inside the pan layer (where panning could not bring it back). The fresh layout is
        // offset by this margin so there is room to drag up and to the left as well as down/right.
        private const double c_WorkspaceMargin = 1000.0;

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

            {
                ReactiveCommand<Unit, Unit> saveArrowGraphImageFileCommand = ReactiveCommand.CreateFromTask(SaveArrowGraphImageFileAsync);
                SaveArrowGraphImageFileCommand = saveArrowGraphImageFileCommand;
            }

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

            // Single live layout pass: the interactive node/edge graph is the on-screen
            // representation. The SVG is built lazily only when exporting (Save As), so a
            // recompile runs the MSAGL layout once rather than twice.
            m_BuildArrowGraphInteractiveSub = this
                .WhenAnyValue(
                    agm => agm.m_CoreViewModel.ArrowGraph,
                    agm => agm.m_CoreViewModel.GraphSettings,
                    agm => agm.m_CoreViewModel.BaseTheme,
                    agm => agm.m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames)
                .MuteWhile(this.WhenAnyValue(agm => agm.m_CoreViewModel.IsBulkUpdating)) // Conflate redundant notifications while a project scenario is loaded/reset.
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await BuildArrowGraphInteractiveAsync());

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

        // Interactive arrow-graph bindings consumed by InteractiveArrowGraphView.
        public ObservableCollection<ArrowGraphNodeViewModel> GraphNodes { get; } = [];

        public ObservableCollection<ArrowGraphEdgeViewModel> GraphEdges { get; } = [];

        private double m_GraphWidth;
        public double GraphWidth
        {
            get => m_GraphWidth;
            private set => this.RaiseAndSetIfChanged(ref m_GraphWidth, value);
        }

        private double m_GraphHeight;
        public double GraphHeight
        {
            get => m_GraphHeight;
            private set => this.RaiseAndSetIfChanged(ref m_GraphHeight, value);
        }

        // The drawable surface size. Larger than the graph (margin on every side) and grown as
        // nodes are dragged outward so the content is never clipped inside the pan layer.
        private double m_WorkspaceWidth;
        public double WorkspaceWidth
        {
            get => m_WorkspaceWidth;
            private set => this.RaiseAndSetIfChanged(ref m_WorkspaceWidth, value);
        }

        private double m_WorkspaceHeight;
        public double WorkspaceHeight
        {
            get => m_WorkspaceHeight;
            private set => this.RaiseAndSetIfChanged(ref m_WorkspaceHeight, value);
        }

        #endregion

        #region Private Methods

        // Rebuild the interactive node/edge view-models from a fresh MSAGL layout.
        private async Task BuildArrowGraphInteractiveAsync()
        {
            try
            {
                GraphLayoutModel layout = BuildLayout();
                Dispatcher.UIThread.Invoke(() => PopulateInteractiveGraph(layout));
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        // Run the MSAGL layout, producing the default node/edge arrangement.
        private GraphLayoutModel BuildLayout()
        {
            lock (m_Lock)
            {
                return HasCompilationErrors
                    ? new GraphLayoutModel()
                    : m_ArrowGraphExport.BuildArrowGraphLayout(
                        GraphPresentationBuilder.ApplyPresentation(m_CoreViewModel.ArrowGraph, m_CoreViewModel.GraphSettings),
                        m_CoreViewModel.BaseTheme,
                        m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames);
            }
        }

        // Discard every dragged position and rebuild from the default MSAGL layout, restoring the
        // arrangement produced on first compilation. Called on the UI thread (context menu).
        public void ResetLayout()
        {
            m_ManualNodePositions.Clear();
            PopulateInteractiveGraph(BuildLayout());
        }

        private void PopulateInteractiveGraph(GraphLayoutModel layout)
        {
            int? previouslySelectedId = m_SelectedNode?.Id;

            foreach (ArrowGraphEdgeViewModel edge in GraphEdges)
            {
                edge.Dispose();
            }
            GraphEdges.Clear();
            GraphNodes.Clear();

            // Drop remembered positions for nodes that no longer exist.
            HashSet<int> layoutIds = [.. layout.Nodes.Select(x => x.Id)];
            foreach (int staleId in m_ManualNodePositions.Keys.Where(x => !layoutIds.Contains(x)).ToList())
            {
                m_ManualNodePositions.Remove(staleId);
            }

            BaseTheme baseTheme = m_CoreViewModel.BaseTheme;

            var nodeLookup = new Dictionary<int, ArrowGraphNodeViewModel>();
            foreach (GraphNodeLayoutModel nodeLayout in layout.Nodes)
            {
                var node = new ArrowGraphNodeViewModel(nodeLayout);

                // Keep a node where the user dragged it; everything else takes the fresh layout,
                // offset by the workspace margin so there is room to drag up and to the left.
                if (m_ManualNodePositions.TryGetValue(node.Id, out (double X, double Y) manual))
                {
                    node.X = manual.X;
                    node.Y = manual.Y;
                }
                else
                {
                    node.X = nodeLayout.X + c_WorkspaceMargin;
                    node.Y = nodeLayout.Y + c_WorkspaceMargin;
                }

                GraphNodes.Add(node);
                nodeLookup[node.Id] = node;
            }

            var adjacency = new Dictionary<int, HashSet<int>>();
            foreach (GraphEdgeLayoutModel edgeLayout in layout.Edges)
            {
                if (!nodeLookup.TryGetValue(edgeLayout.SourceId, out ArrowGraphNodeViewModel? source)
                    || !nodeLookup.TryGetValue(edgeLayout.TargetId, out ArrowGraphNodeViewModel? target))
                {
                    continue;
                }

                GraphEdges.Add(new ArrowGraphEdgeViewModel(
                    edgeLayout.Id,
                    source,
                    target,
                    edgeLayout.StrokeThickness,
                    edgeLayout.IsDashed,
                    edgeLayout.ForegroundColorHexCode,
                    edgeLayout.Label,
                    edgeLayout.ShowLabel,
                    edgeLayout.Tooltip,
                    baseTheme));

                AddAdjacency(adjacency, edgeLayout.SourceId, edgeLayout.TargetId);
                AddAdjacency(adjacency, edgeLayout.TargetId, edgeLayout.SourceId);
            }

            m_Adjacency = adjacency;
            GraphWidth = layout.Width;
            GraphHeight = layout.Height;
            RecomputeWorkspace();

            // Restore the previous selection if that node survived the re-layout.
            if (previouslySelectedId is int selectedId
                && nodeLookup.TryGetValue(selectedId, out ArrowGraphNodeViewModel? reselect))
            {
                SelectNode(reselect);
            }
            else
            {
                SelectNode(null);
            }
        }

        // Remember a node the user has dragged so its position survives the next re-layout.
        public void OnNodeMoved(ArrowGraphNodeViewModel node)
        {
            ArgumentNullException.ThrowIfNull(node);
            m_ManualNodePositions[node.Id] = (node.X, node.Y);
            RecomputeWorkspace();
        }

        // Grow the workspace immediately while a node is being dragged outward, so it never leaves
        // the arrange bounds (and so gets clipped) part way through a drag.
        public void EnsureWorkspaceContains(ArrowGraphNodeViewModel node)
        {
            ArgumentNullException.ThrowIfNull(node);
            double right = node.X + node.Width + c_WorkspaceMargin;
            if (right > WorkspaceWidth)
            {
                WorkspaceWidth = right;
            }
            double bottom = node.Y + node.Height + c_WorkspaceMargin;
            if (bottom > WorkspaceHeight)
            {
                WorkspaceHeight = bottom;
            }
        }

        // Size the workspace to contain every node plus a margin on all sides.
        private void RecomputeWorkspace()
        {
            double maxRight = c_WorkspaceMargin;
            double maxBottom = c_WorkspaceMargin;
            foreach (ArrowGraphNodeViewModel node in GraphNodes)
            {
                maxRight = Math.Max(maxRight, node.X + node.Width);
                maxBottom = Math.Max(maxBottom, node.Y + node.Height);
            }
            WorkspaceWidth = maxRight + c_WorkspaceMargin;
            WorkspaceHeight = maxBottom + c_WorkspaceMargin;
        }

        private static void AddAdjacency(Dictionary<int, HashSet<int>> adjacency, int from, int to)
        {
            if (!adjacency.TryGetValue(from, out HashSet<int>? neighbours))
            {
                neighbours = [];
                adjacency[from] = neighbours;
            }
            neighbours.Add(to);
        }

        // Click-to-select highlighting. Selecting a node emphasises it, its connected edges and its
        // immediate neighbours, and dims everything else.
        public void SelectNode(ArrowGraphNodeViewModel? node)
        {
            m_SelectedNode = node;

            if (node is null)
            {
                foreach (ArrowGraphNodeViewModel candidate in GraphNodes)
                {
                    candidate.IsSelected = false;
                    candidate.IsDimmed = false;
                }
                foreach (ArrowGraphEdgeViewModel edge in GraphEdges)
                {
                    edge.IsHighlighted = false;
                    edge.IsDimmed = false;
                }
                return;
            }

            if (!m_Adjacency.TryGetValue(node.Id, out HashSet<int>? neighbours))
            {
                neighbours = [];
            }

            foreach (ArrowGraphNodeViewModel candidate in GraphNodes)
            {
                bool related = candidate.Id == node.Id || neighbours.Contains(candidate.Id);
                candidate.IsSelected = candidate.Id == node.Id;
                candidate.IsDimmed = !related;
            }

            foreach (ArrowGraphEdgeViewModel edge in GraphEdges)
            {
                bool connected = edge.SourceId == node.Id || edge.TargetId == node.Id;
                edge.IsHighlighted = connected;
                edge.IsDimmed = !connected;
            }
        }

        private async Task SaveArrowGraphImageFileAsync()
        {
            try
            {
                string title = m_SettingService.ProjectTitle;
                title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
                string graphOutputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_ArrowChart}";
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(graphOutputFile, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    await SaveArrowGraphImageFileAsync(filename);
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
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

        public ICommand SaveArrowGraphImageFileCommand { get; }

        public async Task SaveArrowGraphImageFileAsync(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    Resource.ProjectPlan.Messages.Message_EmptyFilename);
            }
            else
            {
                try
                {
                    string fileExtension = Path.GetExtension(filename);
                    byte[]? data = null;
                    bool isSkiaFormat = false;

                    fileExtension.ValueSwitchOn()
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ => isSkiaFormat = true)
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ => isSkiaFormat = true)
                        .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ => isSkiaFormat = true)
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageSvgFileExtension}", _ => isSkiaFormat = true)
                        .Case($".{Resource.ProjectPlan.Filters.Filter_GraphMLFileExtension}", _ =>
                        {
                            data = m_ArrowGraphExport.BuildArrowGraphMLData(GraphPresentationBuilder.ApplyPresentation(m_CoreViewModel.ArrowGraph, m_CoreViewModel.GraphSettings), m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_GraphVizFileExtension}", _ =>
                        {
                            data = m_ArrowGraphExport.BuildArrowGraphVizData(GraphPresentationBuilder.ApplyPresentation(m_CoreViewModel.ArrowGraph, m_CoreViewModel.GraphSettings), m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames);
                        })
                        .Default(_ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Resource.ProjectPlan.Messages.Message_UnableToSaveFile} {filename}"));

                    if (isSkiaFormat)
                    {
                        // Export exactly what is on the interactive canvas (the user's dragged
                        // arrangement), not the default MSAGL SVG layout. The picture is vector, so
                        // SVG/PDF stay crisp while PNG/JPEG render from the same source.
                        SKPicture? picture = null;
                        Dispatcher.UIThread.Invoke(() =>
                            picture = InteractiveArrowGraphRenderer.Render(GraphNodes, GraphEdges));

                        if (picture is not null)
                        {
                            using (picture)
                            {
                                await m_GraphImageExporter.SaveGraphImageAsync(picture, filename, scaleX: 2, scaleY: 2);
                            }
                        }
                    }

                    if (data is not null)
                    {
                        using var stream = File.OpenWrite(filename);
                        await stream.WriteAsync(data);
                    }
                }
                catch (Exception ex)
                {
                    await m_DialogService.ShowErrorAsync(
                        Resource.ProjectPlan.Titles.Title_Error,
                        string.Empty,
                        ex.Message);
                }
            }
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
                        GraphPresentationBuilder.ApplyPresentation(m_CoreViewModel.ArrowGraph, m_CoreViewModel.GraphSettings),
                        m_CoreViewModel.BaseTheme,
                        m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames);
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
            m_BuildArrowGraphInteractiveSub?.Dispose();
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
