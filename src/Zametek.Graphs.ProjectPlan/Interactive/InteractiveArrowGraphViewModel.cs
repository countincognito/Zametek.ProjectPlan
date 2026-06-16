using Avalonia.Threading;
using ReactiveUI;
using SkiaSharp;
using Svg.Skia;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Zametek.Utility;

namespace Zametek.Graphs.ProjectPlan
{
    // The reusable, self-contained interactive arrow graph. It builds the graph, runs the MSAGL
    // layout, populates the draggable/selectable node and edge view-models the InteractiveArrowGraphView
    // binds to, keeps the workspace sized, and exports the graph to file - either from the live
    // interactive canvas or from the fixed MSAGL layout. Everything that is application-specific (the
    // domain graph, the persisted settings, the save dialog and error reporting) is supplied through
    // IArrowGraphHost, so a consumer can drop the control in without writing any of this machinery.
    public class InteractiveArrowGraphViewModel
        : ReactiveObject, IInteractiveArrowGraph, IDisposable
    {
        #region Fields

        private readonly IArrowGraphHost m_Host;
        private readonly IArrowGraphSerializer m_Serializer;
        private readonly IGraphImageExporter m_ImageExporter;

        private readonly IDisposable m_RebuildSub;

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

        public InteractiveArrowGraphViewModel(
            IArrowGraphHost host,
            IArrowGraphSerializer serializer,
            IGraphImageExporter imageExporter)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentNullException.ThrowIfNull(serializer);
            ArgumentNullException.ThrowIfNull(imageExporter);
            m_Host = host;
            m_Serializer = serializer;
            m_ImageExporter = imageExporter;

            SaveArrowGraphImageFileCommand = ReactiveCommand.CreateFromTask(SaveInteractiveImageAsync);

            // The host pre-throttles/schedules the trigger; we simply rebuild on each notification.
            // The notification fires once on subscription, producing the initial layout.
            m_RebuildSub = m_Host.RebuildRequested.Subscribe(_ => Refresh());
        }

        #endregion

        #region Properties

        public GraphTheme Theme => m_Host.Theme;

        public bool ShowNames
        {
            get => m_Host.ShowNames;
            set => m_Host.ShowNames = value;
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

        public ICommand SaveArrowGraphImageFileCommand { get; }

        #endregion

        #region Public Methods

        // Rebuild the interactive node/edge view-models from a fresh MSAGL layout. Called on the
        // host's rebuild notification (off the UI thread); the populate is marshalled to the UI
        // thread. Re-raises Theme/ShowNames so the background and the menu check-box stay in step.
        public void Refresh()
        {
            try
            {
                GraphLayoutModel layout = BuildLayout();
                Dispatcher.UIThread.Invoke(() =>
                {
                    PopulateInteractiveGraph(layout);
                    this.RaisePropertyChanged(nameof(Theme));
                    this.RaisePropertyChanged(nameof(ShowNames));
                });
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(async () => await m_Host.ReportErrorAsync(ex));
            }
        }

        public async Task SaveImageAsync(string? filename, ArrowGraphImageSource source)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                await m_Host.ReportErrorAsync(new ArgumentException(Messages.Message_EmptyFilename));
                return;
            }

            try
            {
                string fileExtension = Path.GetExtension(filename);
                byte[]? data = null;
                bool isImageFormat = false;

                fileExtension.ValueSwitchOn()
                    .Case($".{GraphFileExtensions.Jpeg}", _ => isImageFormat = true)
                    .Case($".{GraphFileExtensions.Png}", _ => isImageFormat = true)
                    .Case($".{GraphFileExtensions.Pdf}", _ => isImageFormat = true)
                    .Case($".{GraphFileExtensions.Svg}", _ => isImageFormat = true)
                    .Case($".{GraphFileExtensions.GraphML}", _ =>
                    {
                        data = m_Serializer.BuildArrowGraphMLData(m_Host.BuildDiagram(multiLineEdgeLabels: true));
                    })
                    .Case($".{GraphFileExtensions.GraphViz}", _ =>
                    {
                        data = m_Serializer.BuildArrowGraphVizData(m_Host.BuildDiagram(multiLineEdgeLabels: true));
                    })
                    .Default(_ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Messages.Message_UnableToSaveFile} {filename}"));

                if (isImageFormat)
                {
                    await SaveImageFormatAsync(filename, fileExtension, source);
                }

                if (data is not null)
                {
                    using var stream = File.OpenWrite(filename);
                    await stream.WriteAsync(data);
                }
            }
            catch (Exception ex)
            {
                await m_Host.ReportErrorAsync(ex);
            }
        }

        // Discard every dragged position and rebuild from the default MSAGL layout, restoring the
        // arrangement produced on first compilation. Called on the UI thread (context menu).
        public void ResetLayout()
        {
            m_ManualNodePositions.Clear();
            PopulateInteractiveGraph(BuildLayout());
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

        #endregion

        #region Private Methods

        // Run the MSAGL layout, producing the default node/edge arrangement.
        private GraphLayoutModel BuildLayout()
        {
            return m_Host.HasCompilationErrors
                ? new GraphLayoutModel()
                : m_Serializer.BuildArrowGraphLayout(
                    m_Host.BuildDiagram(multiLineEdgeLabels: false),
                    m_Host.Theme);
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

            GraphTheme theme = m_Host.Theme;

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
                    theme));

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

        private async Task SaveInteractiveImageAsync()
        {
            try
            {
                string? filename = await m_Host.PickSaveFileAsync();
                if (!string.IsNullOrWhiteSpace(filename))
                {
                    await SaveImageAsync(filename, ArrowGraphImageSource.InteractiveCanvas);
                }
            }
            catch (Exception ex)
            {
                await m_Host.ReportErrorAsync(ex);
            }
        }

        private async Task SaveImageFormatAsync(string filename, string fileExtension, ArrowGraphImageSource source)
        {
            if (source == ArrowGraphImageSource.InteractiveCanvas)
            {
                // Export exactly what is on the interactive canvas (the user's dragged arrangement).
                // The picture is vector, so SVG/PDF stay crisp while PNG/JPEG render from the same
                // source.
                SKPicture? picture = null;
                Dispatcher.UIThread.Invoke(() =>
                    picture = InteractiveArrowGraphRenderer.Render(GraphNodes, GraphEdges));

                if (picture is not null)
                {
                    using (picture)
                    {
                        await m_ImageExporter.SaveGraphImageAsync(picture, filename, scaleX: 2, scaleY: 2);
                    }
                }
                return;
            }

            // FixedLayout: built straight from the diagram, so no interactive surface is needed
            // (this is the path a headless caller such as the CLI uses).
            if (m_Host.HasCompilationErrors)
            {
                return;
            }

            byte[] svgData = m_Serializer.BuildArrowGraphSvgData(
                m_Host.BuildDiagram(multiLineEdgeLabels: false),
                m_Host.Theme);

            if (string.Equals(fileExtension, $".{GraphFileExtensions.Svg}", StringComparison.OrdinalIgnoreCase))
            {
                // Write the MSAGL SVG verbatim so its text stays crisp (no rasterisation).
                using var stream = File.OpenWrite(filename);
                await stream.WriteAsync(svgData);
                return;
            }

            // Rasterise the SVG into a picture for the PNG/JPEG/PDF formats.
            using var svg = new SKSvg();
            using var svgStream = new MemoryStream(svgData);
            svg.Load(svgStream);
            if (svg.Picture is not null)
            {
                await m_ImageExporter.SaveGraphImageAsync(svg.Picture, filename, scaleX: 2, scaleY: 2);
            }
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
                m_RebuildSub.Dispose();
                foreach (ArrowGraphEdgeViewModel edge in GraphEdges)
                {
                    edge.Dispose();
                }
            }

            m_Disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
