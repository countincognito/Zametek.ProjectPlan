using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using SkiaSharp;
using Svg.Skia;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Utility;

namespace Zametek.Graphs.Avalonia
{
    // The reusable, self-contained interactive graph. It builds the graph, runs the MSAGL layout,
    // populates the draggable/selectable node and edge view-models the InteractiveGraphView binds to,
    // keeps the workspace sized, and exports the graph to file - either from the live interactive
    // canvas or from the fixed MSAGL layout. Everything that is application-specific (the domain
    // graph, the persisted settings, the save dialog and error reporting) is supplied through
    // IGraphHost, and the per-graph differences through a GraphConfiguration, so the one view-model
    // serves both the arrow and vertex graphs without a consumer writing any of this machinery.
    // (Replaces the parallel InteractiveArrowGraphViewModel/InteractiveVertexGraphViewModel.)
    public class InteractiveGraphViewModel
        : ReactiveObject, IInteractiveGraph, IDisposable
    {
        #region Fields

        private readonly IGraphHost m_Host;
        private readonly IGraphLayoutEngine m_LayoutEngine;
        private readonly IGraphSerializer m_Serializer;
        private readonly IInteractiveEdgeRouter m_EdgeRouter;

        // The live per-graph configuration (seeded from the preset passed in). It drives the layout/SVG
        // build and the routing-mode menu; the routing-mode command swaps the whole immutable record.
        private GraphConfiguration m_Config;

        private readonly IDisposable m_RebuildSub;

        // Coalesces edge reroutes: starting a new one cancels the previous so only the latest wins.
        private CancellationTokenSource? m_RerouteCts;

        // Re-runs the incoming/outgoing port de-confliction as nodes are dragged (re-armed per layout).
        private IDisposable? m_PortConflictSub;

        private Dictionary<int, HashSet<int>> m_Adjacency = [];
        private GraphNodeViewModel? m_SelectedNode;

        // Positions of nodes the user has dragged, preserved across re-layouts. Also holds positions
        // seeded from a loaded scenario (see SeedNodeLayout), so both are applied as the same overlay.
        private readonly Dictionary<int, (double X, double Y)> m_ManualNodePositions = [];

        // True once the user has actually dragged a node this session. Distinguishes a user-modified
        // arrangement (captured on save) from positions merely seeded from a loaded scenario (which
        // round-trip unchanged). Seeding does not set it; an explicit drag does.
        private bool m_UserHasDragged;

        // The interactive surface (graphCanvas / ItemsControls) is sized to the workspace, not the
        // graph: a fixed margin is added on every side and the workspace grows as nodes are dragged
        // outward, so a dragged node always stays inside the arrange bounds and never gets clipped
        // away inside the pan layer (where panning could not bring it back). The fresh layout is
        // offset by this margin so there is room to drag up and to the left as well as down/right.
        private const double c_WorkspaceMargin = 1000.0;

        #endregion

        #region Ctors

        public InteractiveGraphViewModel(
            IGraphHost host,
            IGraphLayoutEngine layoutEngine,
            IGraphSerializer serializer,
            GraphConfiguration configuration)
            : this(host, layoutEngine, serializer, configuration, edgeRouter: null)
        {
        }

        // Router-injecting overload: supply a custom IInteractiveEdgeRouter (e.g. a future B that keeps a
        // persistent live router and reroutes only the dragged node's incident edges) or pass null to use
        // the default MSAGL router. Also used by the tests.
        public InteractiveGraphViewModel(
            IGraphHost host,
            IGraphLayoutEngine layoutEngine,
            IGraphSerializer serializer,
            GraphConfiguration configuration,
            IInteractiveEdgeRouter? edgeRouter)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentNullException.ThrowIfNull(layoutEngine);
            ArgumentNullException.ThrowIfNull(serializer);
            ArgumentNullException.ThrowIfNull(configuration);
            m_Host = host;
            m_LayoutEngine = layoutEngine;
            m_Serializer = serializer;
            m_Config = configuration;
            // Defaulted (not injected) so the manager view-models stay simple; a future B (live
            // rerouting) can inject a persistent router behind the same interface.
            m_EdgeRouter = edgeRouter ?? new MsaglInteractiveEdgeRouter();

            SaveGraphImageFileCommand = ReactiveCommand.CreateFromTask(SaveInteractiveImageAsync);
            ChangeEdgeRoutingModeCommand = ReactiveCommand.Create<GraphEdgeRoutingMode>(ChangeEdgeRoutingMode);

            // The host pre-throttles/schedules the trigger; we simply rebuild on each notification.
            // The notification fires once on subscription, producing the initial layout.
            m_RebuildSub = m_Host.RebuildRequested.Subscribe(_ => Refresh());
        }

        #endregion

        #region Properties

        public GraphTheme Theme => m_Host.Theme;

        // The live per-graph configuration (seeded from the preset; the routing-mode command swaps the
        // whole record). Exposed so the host's data path can build the fixed SVG with the same live
        // config the interactive view uses.
        public GraphConfiguration Configuration => m_Config;

        public bool SupportsShowNames => m_Config.SupportsShowNames;

        // The current edge routing mode (the menu's radio items read this); set via
        // ChangeEdgeRoutingModeCommand.
        public GraphEdgeRoutingMode EdgeRoutingMode => m_Config.EdgeRoutingMode;

        public bool ShowNames
        {
            get => m_Host.ShowNames;
            set => m_Host.ShowNames = value;
        }

        // Interactive graph bindings consumed by InteractiveGraphView.
        public ObservableCollection<GraphNodeViewModel> GraphNodes { get; } = [];

        public ObservableCollection<GraphEdgeViewModel> GraphEdges { get; } = [];

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

        // Persisted viewport transform (see IInteractiveGraph). Plain stored state owned by the
        // InteractiveGraphView, which writes on zoom/pan/fit and reads back when its control is
        // rebuilt; nothing binds to these, so no change notification is needed.
        public double ViewZoom { get; set; }

        public double ViewPanX { get; set; }

        public double ViewPanY { get; set; }

        public bool HasViewState { get; set; }

        public ICommand SaveGraphImageFileCommand { get; }

        public ICommand ChangeEdgeRoutingModeCommand { get; }

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
                    RerouteEdges();
                    GraphRefreshed?.Invoke(this, EventArgs.Empty);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(async () => await m_Host.ReportErrorAsync(ex));
            }
        }

        public async Task SaveImageAsync(string? filename, GraphImageSource source)
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
                        data = m_Serializer.BuildGraphMLData(m_Host.BuildDiagram(multiLineEdgeLabels: true));
                    })
                    .Case($".{GraphFileExtensions.GraphViz}", _ =>
                    {
                        data = m_Serializer.BuildGraphVizData(m_Host.BuildDiagram(multiLineEdgeLabels: true));
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
            m_UserHasDragged = false;
            PopulateInteractiveGraph(BuildLayout());
            RerouteEdges();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        // Reset the persisted viewport so the next graph is framed from scratch: zoom back to x1, pan to
        // the origin, clear HasViewState. Raises ViewReset so an attached view drops its live transform;
        // a detached view picks it up via HasViewState when it next attaches.
        public void ResetView()
        {
            ViewZoom = 1.0;
            ViewPanX = 0.0;
            ViewPanY = 0.0;
            HasViewState = false;
            ViewReset?.Invoke(this, EventArgs.Empty);
        }

        // Remember a node the user has dragged so its position survives the next re-layout.
        public void OnNodeMoved(GraphNodeViewModel node)
        {
            ArgumentNullException.ThrowIfNull(node);
            m_ManualNodePositions[node.Id] = (node.X, node.Y);
            m_UserHasDragged = true;
            RecomputeWorkspace();
            // Drag-end: re-route the edges for the dropped arrangement (the B' trigger). During the drag
            // the moved edges fell back to the approximation; this upgrades them back to exact geometry.
            RerouteEdges();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        // --- Layout persistence: the host saves/restores the arrangement through these (the View
        // contract IInteractiveGraph stays minimal; the host holds the concrete view-model). ---

        // Raised when the user changes the arrangement (a drag-end or a reset), so the host can capture
        // the new layout for persistence. Seeding a saved layout does NOT raise it.
        public event EventHandler? LayoutChanged;

        // Raised when ResetView clears the viewport (project scenario reset/closed), so an attached
        // InteractiveGraphView drops its live zoom/pan and re-frames the next graph from scratch.
        public event EventHandler? ViewReset;

        // Raised after the graph is rebuilt or a saved layout is seeded, so an attached view can re-frame
        // a fresh load even when the workspace size is unchanged (e.g. switching between scenarios with an
        // identical graph but different layouts). The view coalesces and defers it so it runs after seed.
        public event EventHandler? GraphRefreshed;

        // True once the user has manually dragged a node this session, so a save captures the live
        // arrangement rather than round-tripping the layout that was loaded.
        public bool HasManualLayout => m_UserHasDragged;

        // The current node arrangement in layout space (the workspace margin removed), for persistence.
        public IReadOnlyList<GraphNodePosition> GetNodeLayout()
        {
            return [.. GraphNodes.Select(n => new GraphNodePosition(n.Id, n.X - c_WorkspaceMargin, n.Y - c_WorkspaceMargin))];
        }

        // Seed a saved arrangement (layout space) as a best-effort overlay: nodes take their saved
        // positions on the next build (and immediately, if already shown); ids no longer present are
        // dropped by the build's reconciliation, and nodes without a saved position keep the fresh
        // layout. Seeding is not a manual drag, so it leaves HasManualLayout false.
        public void SeedNodeLayout(IReadOnlyList<GraphNodePosition> positions)
        {
            ArgumentNullException.ThrowIfNull(positions);
            m_ManualNodePositions.Clear();
            foreach (GraphNodePosition position in positions)
            {
                m_ManualNodePositions[position.Id] = (position.X + c_WorkspaceMargin, position.Y + c_WorkspaceMargin);
            }
            m_UserHasDragged = false;

            // Apply to any nodes already on screen so a seed after the build still takes effect.
            bool applied = false;
            foreach (GraphNodeViewModel node in GraphNodes)
            {
                if (m_ManualNodePositions.TryGetValue(node.Id, out (double X, double Y) seeded))
                {
                    node.X = seeded.X;
                    node.Y = seeded.Y;
                    applied = true;
                }
            }
            if (applied)
            {
                RecomputeWorkspace();
                RerouteEdges();
            }

            // The seed is the final step of a scenario load, so signal a re-frame here too: an identical
            // graph with a different saved layout changes node positions but not the workspace size, so
            // the view's size-driven auto-fit would not otherwise fire.
            GraphRefreshed?.Invoke(this, EventArgs.Empty);
        }

        // Apply a saved edge routing mode (e.g. when a scenario is loaded), swapping the configuration
        // and updating the existing edges exactly as the context-menu command does.
        public void ApplyEdgeRoutingMode(GraphEdgeRoutingMode mode)
        {
            ChangeEdgeRoutingMode(mode);
        }

        // Grow the workspace immediately while a node is being dragged outward, so it never leaves
        // the arrange bounds (and so gets clipped) part way through a drag.
        public void EnsureWorkspaceContains(GraphNodeViewModel node)
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
        public void SelectNode(GraphNodeViewModel? node)
        {
            m_SelectedNode = node;

            if (node is null)
            {
                foreach (GraphNodeViewModel candidate in GraphNodes)
                {
                    candidate.IsSelected = false;
                    candidate.IsDimmed = false;
                }
                foreach (GraphEdgeViewModel edge in GraphEdges)
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

            foreach (GraphNodeViewModel candidate in GraphNodes)
            {
                bool related = candidate.Id == node.Id || neighbours.Contains(candidate.Id);
                candidate.IsSelected = candidate.Id == node.Id;
                candidate.IsDimmed = !related;
            }

            foreach (GraphEdgeViewModel edge in GraphEdges)
            {
                bool connected = edge.SourceId == node.Id || edge.TargetId == node.Id;
                edge.IsHighlighted = connected;
                edge.IsDimmed = !connected;
            }
        }

        #endregion

        #region Private Methods

        // Switch the edge routing mode. The whole configuration record is swapped on the view-model (so
        // a subsequent layout/SVG export follows the new mode), and the existing edges are updated in
        // place - node positions are independent of the routing mode, so no re-layout is needed. Raising
        // EdgeRoutingMode lets the context menu's radio items re-evaluate.
        private void ChangeEdgeRoutingMode(GraphEdgeRoutingMode routingMode)
        {
            if (m_Config.EdgeRoutingMode == routingMode)
            {
                return;
            }

            m_Config = m_Config with { EdgeRoutingMode = routingMode };
            foreach (GraphEdgeViewModel edge in GraphEdges)
            {
                // Setting the mode drops any exact routed geometry (it was for the old mode), so the
                // edges show the new mode's approximation immediately; the reroute below upgrades them.
                edge.RoutingMode = routingMode;
            }
            this.RaisePropertyChanged(nameof(EdgeRoutingMode));
            RerouteEdges();
        }

        // Replace the client-side edge approximations with exact MSAGL-routed geometry for the current
        // node positions and mode. This is the B' seam: it is invoked once whenever the arrangement
        // settles (initial layout, reset, mode change, drag-end). Transitioning to B (live rerouting)
        // is just calling this more often - throttled, from the per-move drag path - plus optionally
        // injecting a persistent IInteractiveEdgeRouter that reroutes only the dragged edges. The
        // snapshot is taken synchronously on the UI thread; the routing runs off-thread; the result is
        // applied back on the UI thread (the await resumes on the captured UI context).
        private void RerouteEdges()
        {
            _ = RerouteEdgesAsync();
        }

        private async Task RerouteEdgesAsync()
        {
            EdgeRoutingRequest request = SnapshotRouting();
            if (request.Edges.Count == 0)
            {
                return;
            }

            // Supersede any in-flight reroute so only the latest arrangement is applied.
            m_RerouteCts?.Cancel();
            var cts = new CancellationTokenSource();
            m_RerouteCts = cts;

            try
            {
                IReadOnlyList<RoutedEdge> routed = await m_EdgeRouter.RouteAsync(request, cts.Token);
                if (!cts.Token.IsCancellationRequested)
                {
                    ApplyRoutedEdges(routed);
                }
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer reroute; nothing to apply.
            }
            catch (Exception ex)
            {
                await m_Host.ReportErrorAsync(ex);
            }
            finally
            {
                if (ReferenceEquals(m_RerouteCts, cts))
                {
                    m_RerouteCts = null;
                }
                cts.Dispose();
            }
        }

        // Immutable screen-coordinate snapshot of the current nodes, edges and mode, for off-thread
        // routing.
        private EdgeRoutingRequest SnapshotRouting()
        {
            var nodes = new List<EdgeRoutingNode>(GraphNodes.Count);
            foreach (GraphNodeViewModel node in GraphNodes)
            {
                nodes.Add(new EdgeRoutingNode(node.Id, node.X, node.Y, node.Width, node.Height));
            }

            var edges = new List<EdgeRoutingEdge>(GraphEdges.Count);
            foreach (GraphEdgeViewModel edge in GraphEdges)
            {
                edges.Add(new EdgeRoutingEdge(edge.Id, edge.SourceId, edge.TargetId));
            }

            return new EdgeRoutingRequest(nodes, edges, EdgeRoutingMode);
        }

        // Apply exact routed geometry to the matching edges. Edges absent from the result (e.g. when the
        // mode needs no routing, or routing failed) keep whatever geometry they currently show.
        private void ApplyRoutedEdges(IReadOnlyList<RoutedEdge> routed)
        {
            if (routed.Count == 0)
            {
                return;
            }

            Dictionary<int, GraphEdgeViewModel> byId = GraphEdges.ToDictionary(x => x.Id);
            foreach (RoutedEdge routedEdge in routed)
            {
                if (byId.TryGetValue(routedEdge.Id, out GraphEdgeViewModel? edge))
                {
                    edge.SetRoutedSegments(routedEdge.Segments);
                }
            }
        }

        // Run the MSAGL layout, producing the default node/edge arrangement.
        private GraphLayoutModel BuildLayout()
        {
            return m_Host.HasCompilationErrors
                ? new GraphLayoutModel()
                : m_LayoutEngine.BuildLayout(m_Host.BuildDiagram(multiLineEdgeLabels: false), m_Config, m_Host.Theme);
        }

        private void PopulateInteractiveGraph(GraphLayoutModel layout)
        {
            int? previouslySelectedId = m_SelectedNode?.Id;

            foreach (GraphEdgeViewModel edge in GraphEdges)
            {
                edge.Dispose();
            }
            GraphEdges.Clear();
            GraphNodes.Clear();

            // Drop remembered positions for nodes that no longer exist - but only when we have a real
            // layout to reconcile against. An empty layout is a transient state (the graph rebuilds
            // empty before compilation completes, and on a compilation error); treating it as "every
            // node was removed" would wipe positions just seeded from a loaded scenario, before the
            // real layout arrives in a follow-up populate. Reconcile only against a populated layout.
            if (layout.Nodes.Count > 0)
            {
                HashSet<int> layoutIds = [.. layout.Nodes.Select(x => x.Id)];
                foreach (int staleId in m_ManualNodePositions.Keys.Where(x => !layoutIds.Contains(x)).ToList())
                {
                    m_ManualNodePositions.Remove(staleId);
                }
            }

            GraphTheme theme = m_Host.Theme;

            var nodeLookup = new Dictionary<int, GraphNodeViewModel>();
            foreach (GraphNodeLayoutModel nodeLayout in layout.Nodes)
            {
                var node = new GraphNodeViewModel(nodeLayout);

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
                if (!nodeLookup.TryGetValue(edgeLayout.SourceId, out GraphNodeViewModel? source)
                    || !nodeLookup.TryGetValue(edgeLayout.TargetId, out GraphNodeViewModel? target))
                {
                    continue;
                }

                GraphEdges.Add(new GraphEdgeViewModel(
                    edgeLayout.Id,
                    source,
                    target,
                    edgeLayout.StrokeThickness,
                    edgeLayout.IsDashed,
                    edgeLayout.ForegroundColorHexCode,
                    edgeLayout.Label,
                    edgeLayout.ShowLabel,
                    edgeLayout.Tooltip,
                    theme,
                    EdgeRoutingMode));

                AddAdjacency(adjacency, edgeLayout.SourceId, edgeLayout.TargetId);
                AddAdjacency(adjacency, edgeLayout.TargetId, edgeLayout.SourceId);
            }

            m_Adjacency = adjacency;
            GraphWidth = layout.Width;
            GraphHeight = layout.Height;
            RecomputeWorkspace();

            // Restore the previous selection if that node survived the re-layout.
            if (previouslySelectedId is int selectedId
                && nodeLookup.TryGetValue(selectedId, out GraphNodeViewModel? reselect))
            {
                SelectNode(reselect);
            }
            else
            {
                SelectNode(null);
            }

            // Re-arm the drag-time port de-confliction for the new node/edge set.
            SubscribeToPortConflicts();
        }

        // Subscribe to node moves so the port resolver re-runs during a drag. Skip(1) drops each node's
        // initial value, so populating the graph does not trigger a burst of resolutions; thereafter
        // only the dragged node emits.
        private void SubscribeToPortConflicts()
        {
            m_PortConflictSub?.Dispose();
            if (GraphNodes.Count == 0)
            {
                m_PortConflictSub = null;
                return;
            }
            m_PortConflictSub = Observable
                .Merge(GraphNodes.Select(node => node.WhenAnyValue(x => x.X, x => x.Y).Skip(1)))
                .Subscribe(_ => ResolvePortConflicts());
        }

        // Arrange the edge ports for the current arrangement: steer each edge clear of the nodes in its
        // way (GraphClashResolver) and separate the edges that share a node side so their ports do not
        // overlap (GraphPortOffsetResolver). Rectilinear-only and only visible during a drag (at rest the
        // exact routed geometry is shown); for other modes the overrides/offsets are cleared so each edge
        // keeps its own resolve. The snapshot is taken on the UI thread; the resolvers do the work.
        private void ResolvePortConflicts()
        {
            if (!GraphEdgeGeometry.IsRectilinear(EdgeRoutingMode))
            {
                foreach (GraphEdgeViewModel edge in GraphEdges)
                {
                    edge.ClearResolvedRoute();
                    edge.ClearPortOffsets();
                }
                return;
            }
            if (GraphEdges.Count == 0)
            {
                return;
            }

            var nodes = new List<PortNode>(GraphNodes.Count);
            foreach (GraphNodeViewModel node in GraphNodes)
            {
                nodes.Add(new PortNode(node.Id, node.CentreX, node.CentreY));
            }

            var edges = new List<PortEdge>(GraphEdges.Count);
            foreach (GraphEdgeViewModel edge in GraphEdges)
            {
                (GraphConnectionAxis source, GraphConnectionAxis target) = edge.TentativeConnectionAxes;
                edges.Add(new PortEdge(edge.Id, edge.SourceId, edge.TargetId, source, target));
            }

            // Node sizes are uniform in the arrow/vertex graphs, so one width/height suffices.
            double nodeWidth = GraphNodes[0].Width;
            double nodeHeight = GraphNodes[0].Height;

            var nodeById = new Dictionary<int, PortNode>(nodes.Count);
            foreach (PortNode node in nodes)
            {
                nodeById[node.Id] = node;
            }

            // Steer each edge around the nodes it is not connected to (clash avoidance), then snapshot the
            // resolved route's actual attach points so the port offsetting can spread EVERY edge sharing a
            // node side - L/Z and the Bracket/Saucepan detours alike (the detour ends can leave on a side
            // facing away from the far node, so they are grouped by where they actually attach).
            var resolvedById = new Dictionary<int, GraphRoutePlan>(edges.Count);
            var placements = new List<PortPlacement>(edges.Count);
            foreach (PortEdge edge in edges)
            {
                if (nodeById.TryGetValue(edge.SourceId, out PortNode sourceNode)
                    && nodeById.TryGetValue(edge.TargetId, out PortNode targetNode))
                {
                    GraphRoutePlan route =
                        GraphClashResolver.Resolve(sourceNode, targetNode, edge.SourceAxis, edge.TargetAxis, nodes, nodeWidth, nodeHeight);
                    resolvedById[edge.Id] = route;
                    IReadOnlyList<Point> corners = GraphEdgeGeometry.RouteCorners(
                        new Point(sourceNode.CentreX, sourceNode.CentreY),
                        new Point(targetNode.CentreX, targetNode.CentreY),
                        nodeWidth, nodeHeight, route);
                    placements.Add(new PortPlacement(edge.Id, edge.SourceId, edge.TargetId, corners[0], corners[^1]));
                }
                else
                {
                    resolvedById[edge.Id] = new GraphRoutePlan(edge.SourceAxis, edge.TargetAxis);
                }
            }

            IReadOnlyDictionary<int, (Point SourceOffset, Point TargetOffset)> offsets =
                GraphPortOffsetResolver.Resolve(nodes, placements, nodeWidth, nodeHeight);

            foreach (GraphEdgeViewModel edge in GraphEdges)
            {
                edge.SetResolvedRoute(resolvedById[edge.Id]);
                if (offsets.TryGetValue(edge.Id, out (Point SourceOffset, Point TargetOffset) offset))
                {
                    edge.SetPortOffsets(offset.SourceOffset, offset.TargetOffset);
                }
                else
                {
                    edge.ClearPortOffsets();
                }
            }
        }

        // Size the workspace to contain every node plus a margin on all sides.
        private void RecomputeWorkspace()
        {
            double maxRight = c_WorkspaceMargin;
            double maxBottom = c_WorkspaceMargin;
            foreach (GraphNodeViewModel node in GraphNodes)
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
                    await SaveImageAsync(filename, GraphImageSource.InteractiveCanvas);
                }
            }
            catch (Exception ex)
            {
                await m_Host.ReportErrorAsync(ex);
            }
        }

        private async Task SaveImageFormatAsync(string filename, string fileExtension, GraphImageSource source)
        {
            if (source == GraphImageSource.InteractiveCanvas)
            {
                // Export exactly what is on the interactive canvas (the user's dragged arrangement).
                // The picture is vector, so SVG/PDF stay crisp while PNG/JPEG render from the same
                // source.
                SKPicture? picture = null;
                Dispatcher.UIThread.Invoke(() =>
                    picture = InteractiveGraphRenderer.Render(GraphNodes, GraphEdges));

                if (picture is not null)
                {
                    using (picture)
                    {
                        await GraphImageExporter.SaveGraphImageAsync(picture, filename, scaleX: 2, scaleY: 2);
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

            byte[] svgData = m_LayoutEngine.RenderSvg(
                m_Host.BuildDiagram(multiLineEdgeLabels: false),
                m_Config,
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
                await GraphImageExporter.SaveGraphImageAsync(svg.Picture, filename, scaleX: 2, scaleY: 2);
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
                m_PortConflictSub?.Dispose();
                m_RerouteCts?.Cancel();
                foreach (GraphEdgeViewModel edge in GraphEdges)
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
