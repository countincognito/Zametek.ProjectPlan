using Avalonia.Svg.Skia;
using Avalonia.Threading;
using ReactiveUI;
using SkiaSharp;
using Svg.Skia;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class VertexGraphManagerViewModel
        : ToolViewModelBase, IVertexGraphManagerViewModel
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
        private readonly IVertexGraphSerializer m_VertexGraphExport;
        private readonly IGraphImageExporter m_GraphImageExporter;

        private readonly IDisposable? m_BuildVertexGraphInteractiveSub;

        // Interactive vertex-graph state.
        private Dictionary<int, HashSet<int>> m_Adjacency = [];
        private VertexGraphNodeViewModel? m_SelectedNode;

        // Positions of nodes the user has dragged, preserved across re-layouts.
        private readonly Dictionary<int, (double X, double Y)> m_ManualNodePositions = [];

        #endregion

        #region Ctors

        public VertexGraphManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService,
            IVertexGraphSerializer vertexGraphExport,
            IGraphImageExporter graphImageExporter)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(vertexGraphExport);
            ArgumentNullException.ThrowIfNull(graphImageExporter);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_VertexGraphExport = vertexGraphExport;
            m_GraphImageExporter = graphImageExporter;

            m_VertexGraphData = string.Empty;
            m_VertexGraphImage = new SvgImage();

            {
                ReactiveCommand<Unit, Unit> saveVertexGraphImageFileCommand = ReactiveCommand.CreateFromTask(SaveVertexGraphImageFileAsync);
                SaveVertexGraphImageFileCommand = saveVertexGraphImageFileCommand;
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
                .WhenAnyValue(agm => agm.m_CoreViewModel.DisplaySettingsViewModel.VertexGraphShowNames)
                .ToProperty(this, agm => agm.ShowNames);

            m_BaseTheme = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.BaseTheme)
                .ToProperty(this, agm => agm.BaseTheme);

            // Single live layout pass: the interactive node/edge graph is the on-screen
            // representation. The SVG is built lazily only when exporting (Save As), so a
            // recompile runs the MSAGL layout once rather than twice.
            m_BuildVertexGraphInteractiveSub = this
                .WhenAnyValue(
                    agm => agm.m_CoreViewModel.VertexGraph,
                    agm => agm.m_CoreViewModel.GraphSettings,
                    agm => agm.m_CoreViewModel.BaseTheme,
                    agm => agm.m_CoreViewModel.DisplaySettingsViewModel.VertexGraphShowNames)
                .MuteWhile(this.WhenAnyValue(agm => agm.m_CoreViewModel.IsBulkUpdating))
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await BuildVertexGraphInteractiveAsync());

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

        // Spike: interactive vertex-graph bindings consumed by VertexGraphManagerView.
        public ObservableCollection<VertexGraphNodeViewModel> GraphNodes { get; } = [];

        public ObservableCollection<VertexGraphEdgeViewModel> GraphEdges { get; } = [];

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

        #endregion

        #region Private Methods

        // Rebuild the interactive node/edge view-models from a fresh MSAGL layout.
        private async Task BuildVertexGraphInteractiveAsync()
        {
            try
            {
                GraphLayoutModel layout;
                lock (m_Lock)
                {
                    layout = HasCompilationErrors
                        ? new GraphLayoutModel()
                        : m_VertexGraphExport.BuildVertexGraphLayout(
                            m_CoreViewModel.VertexGraph,
                            m_CoreViewModel.GraphSettings,
                            m_CoreViewModel.BaseTheme,
                            m_CoreViewModel.DisplaySettingsViewModel.VertexGraphShowNames);
                }

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

        private void PopulateInteractiveGraph(GraphLayoutModel layout)
        {
            int? previouslySelectedId = m_SelectedNode?.Id;

            foreach (VertexGraphEdgeViewModel edge in GraphEdges)
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

            var nodeLookup = new Dictionary<int, VertexGraphNodeViewModel>();
            foreach (GraphNodeLayoutModel nodeLayout in layout.Nodes)
            {
                var node = new VertexGraphNodeViewModel(nodeLayout);

                // Keep a node where the user dragged it; everything else takes the fresh layout.
                if (m_ManualNodePositions.TryGetValue(node.Id, out (double X, double Y) manual))
                {
                    node.X = manual.X;
                    node.Y = manual.Y;
                }

                GraphNodes.Add(node);
                nodeLookup[node.Id] = node;
            }

            var adjacency = new Dictionary<int, HashSet<int>>();
            foreach (GraphEdgeLayoutModel edgeLayout in layout.Edges)
            {
                if (!nodeLookup.TryGetValue(edgeLayout.SourceId, out VertexGraphNodeViewModel? source)
                    || !nodeLookup.TryGetValue(edgeLayout.TargetId, out VertexGraphNodeViewModel? target))
                {
                    continue;
                }

                GraphEdges.Add(new VertexGraphEdgeViewModel(edgeLayout.Id, source, target, edgeLayout.StrokeThickness, edgeLayout.IsDashed));

                AddAdjacency(adjacency, edgeLayout.SourceId, edgeLayout.TargetId);
                AddAdjacency(adjacency, edgeLayout.TargetId, edgeLayout.SourceId);
            }

            m_Adjacency = adjacency;
            GraphWidth = layout.Width;
            GraphHeight = layout.Height;

            // Restore the previous selection if that node survived the re-layout.
            if (previouslySelectedId is int selectedId
                && nodeLookup.TryGetValue(selectedId, out VertexGraphNodeViewModel? reselect))
            {
                SelectNode(reselect);
            }
            else
            {
                SelectNode(null);
            }
        }

        // Remember a node the user has dragged so its position survives the next re-layout.
        public void OnNodeMoved(VertexGraphNodeViewModel node)
        {
            ArgumentNullException.ThrowIfNull(node);
            m_ManualNodePositions[node.Id] = (node.X, node.Y);
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

        // Spike: click-to-select highlighting. Selecting a node emphasises it, its
        // connected edges and its immediate neighbours, and dims everything else.
        public void SelectNode(VertexGraphNodeViewModel? node)
        {
            m_SelectedNode = node;

            if (node is null)
            {
                foreach (VertexGraphNodeViewModel candidate in GraphNodes)
                {
                    candidate.IsSelected = false;
                    candidate.IsDimmed = false;
                }
                foreach (VertexGraphEdgeViewModel edge in GraphEdges)
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

            foreach (VertexGraphNodeViewModel candidate in GraphNodes)
            {
                bool related = candidate.Id == node.Id || neighbours.Contains(candidate.Id);
                candidate.IsSelected = candidate.Id == node.Id;
                candidate.IsDimmed = !related;
            }

            foreach (VertexGraphEdgeViewModel edge in GraphEdges)
            {
                bool connected = edge.SourceId == node.Id || edge.TargetId == node.Id;
                edge.IsHighlighted = connected;
                edge.IsDimmed = !connected;
            }
        }

        private async Task SaveVertexGraphImageFileAsync()
        {
            try
            {
                string title = m_SettingService.ProjectTitle;
                title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
                string graphOutputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_VertexChart}";
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(graphOutputFile, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    await SaveVertexGraphImageFileAsync(filename);
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

        #region IVertexGraphManagerViewModel Members

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
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.VertexGraphShowNames = value;
            }
        }

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

        public ICommand SaveVertexGraphImageFileCommand { get; }

        public async Task SaveVertexGraphImageFileAsync(string? filename)
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
                            data = m_VertexGraphExport.BuildVertexGraphMLData(m_CoreViewModel.VertexGraph, m_CoreViewModel.GraphSettings, m_CoreViewModel.DisplaySettingsViewModel.VertexGraphShowNames);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_GraphVizFileExtension}", _ =>
                        {
                            data = m_VertexGraphExport.BuildVertexGraphVizData(m_CoreViewModel.VertexGraph, m_CoreViewModel.GraphSettings, m_CoreViewModel.DisplaySettingsViewModel.VertexGraphShowNames);
                        })
                        .Default(_ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Resource.ProjectPlan.Messages.Message_UnableToSaveFile} {filename}"));

                    if (isSkiaFormat)
                    {
                        // The interactive view no longer keeps the SVG image current, so build it
                        // on demand here (the only place a rendered raster/PDF/SVG is needed).
                        BuildVertexGraphDiagramData();
                        BuildVertexGraphDiagramImage();

                        if (VertexGraphImage.Source?.Picture is SKPicture picture)
                        {
                            await m_GraphImageExporter.SaveGraphImageAsync(picture, filename, scaleX: 4, scaleY: 4);
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

        public void BuildVertexGraphDiagramData()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(VertexGraphManagerViewModel)}.{nameof(BuildVertexGraphDiagramData)}");
            byte[]? data = null;

            lock (m_Lock)
            {
                if (!HasCompilationErrors)
                {
                    data = m_VertexGraphExport.BuildVertexGraphSvgData(
                        m_CoreViewModel.VertexGraph,
                        m_CoreViewModel.GraphSettings,
                        m_CoreViewModel.BaseTheme,
                        m_CoreViewModel.DisplaySettingsViewModel.VertexGraphShowNames);
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
            m_BuildVertexGraphInteractiveSub?.Dispose();
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
