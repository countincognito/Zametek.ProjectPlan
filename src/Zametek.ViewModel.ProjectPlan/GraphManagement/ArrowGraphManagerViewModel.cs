using AutoMapper;
using Prism;
using Prism.Commands;
using Prism.Events;
using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Event.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ArrowGraphManagerViewModel
        : PropertyChangedPubSubViewModel, IArrowGraphManagerViewModel, IActiveAware
    {
        #region Fields

        private readonly object m_Lock;

        private ArrowGraphData m_ArrowGraphData;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IProjectService m_ProjectService;
        private readonly IMapper m_Mapper;
        private readonly IEventAggregator m_EventService;

        private readonly InteractionRequest<Notification> m_NotificationInteractionRequest;

        private SubscriptionToken m_GraphCompiledSubscriptionToken;
        private SubscriptionToken m_ArrowGraphSettingsUpdatedSubscriptionToken;
        private SubscriptionToken m_ArrowGraphUpdatedSubscriptionToken;

        private bool m_IsActive;

        #endregion

        #region Ctors

        public ArrowGraphManagerViewModel(
            ICoreViewModel coreViewModel,
            IProjectService projectService,
            IMapper mapper,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_ProjectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            m_Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            m_NotificationInteractionRequest = new InteractionRequest<Notification>();

            InitializeCommands();
            SubscribeToEvents();

            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.IsBusy), nameof(IsBusy), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.HasStaleOutputs), nameof(HasStaleArrowGraph), ThreadOption.BackgroundThread);
        }

        #endregion

        #region Properties

        private ArrowGraphSettingsModel ArrowGraphSettings => m_CoreViewModel.ArrowGraphSettings;

        private bool HasStaleOutputs => m_CoreViewModel.HasStaleOutputs;

        private ArrowGraphModel ArrowGraph
        {
            get
            {
                return m_CoreViewModel.ArrowGraph;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.ArrowGraph = value;
                }
            }
        }

        private bool IsProjectUpdated
        {
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.IsProjectUpdated = value;
                }
            }
        }

        private bool HasCompilationErrors => m_CoreViewModel.HasCompilationErrors;

        private IGraphCompilation<int, int, IDependentActivity<int, int>> GraphCompilation => m_CoreViewModel.GraphCompilation;

        #endregion

        #region Commands

        private DelegateCommandBase InternalGenerateArrowGraphCommand
        {
            get;
            set;
        }

        private async void GenerateArrowGraph()
        {
            await DoGenerateArrowGraphAsync().ConfigureAwait(true);
        }

        private bool CanGenerateArrowGraph()
        {
            return !HasCompilationErrors;
        }

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            GenerateArrowGraphCommand =
                InternalGenerateArrowGraphCommand =
                    new DelegateCommand(GenerateArrowGraph, CanGenerateArrowGraph);
        }

        private void RaiseCanExecuteChangedAllCommands()
        {
            InternalGenerateArrowGraphCommand.RaiseCanExecuteChanged();
        }

        private void SubscribeToEvents()
        {
            m_GraphCompiledSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<GraphCompiledPayload>>()
                    .Subscribe(payload =>
                    {
                        HasStaleArrowGraph = true;
                    }, ThreadOption.BackgroundThread);
            m_ArrowGraphSettingsUpdatedSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<ArrowGraphSettingsUpdatedPayload>>()
                    .Subscribe(payload =>
                    {
                        HasStaleArrowGraph = true;
                    }, ThreadOption.BackgroundThread);
            m_ArrowGraphUpdatedSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<ArrowGraphUpdatedPayload>>()
                    .Subscribe(async payload =>
                    {
                        await GenerateArrowGraphDataFromAsync().ConfigureAwait(true);
                    }, ThreadOption.BackgroundThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompiledPayload>>()
                .Unsubscribe(m_GraphCompiledSubscriptionToken);
            m_EventService.GetEvent<PubSubEvent<ArrowGraphSettingsUpdatedPayload>>()
                .Unsubscribe(m_ArrowGraphSettingsUpdatedSubscriptionToken);
            m_EventService.GetEvent<PubSubEvent<ArrowGraphUpdatedPayload>>()
                .Unsubscribe(m_ArrowGraphUpdatedSubscriptionToken);
        }

        private void PublishArrowGraphDataUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<ArrowGraphDataUpdatedPayload>>()
                .Publish(new ArrowGraphDataUpdatedPayload());
        }

        private async Task GenerateArrowGraphFromGraphCompilationAsync()
        {
            await Task.Run(() => GenerateArrowGraphFromGraphCompilation()).ConfigureAwait(true);
        }

        private void GenerateArrowGraphFromGraphCompilation()
        {
            lock (m_Lock)
            {
                ArrowGraph = null;
                IList<IDependentActivity<int, int>> dependentActivities =
                    GraphCompilation.DependentActivities
                    .Select(x => (IDependentActivity<int, int>)x.CloneObject())
                    .ToList();

                if (!HasCompilationErrors
                    && dependentActivities.Any())
                {
                    var arrowGraphCompiler = new ArrowGraphCompiler<int, int, IDependentActivity<int, int>>();
                    foreach (IDependentActivity<int, int> dependentActivity in dependentActivities)
                    {
                        dependentActivity.Dependencies.UnionWith(dependentActivity.ResourceDependencies);
                        dependentActivity.ResourceDependencies.Clear();
                        arrowGraphCompiler.AddActivity(dependentActivity);
                    }

                    arrowGraphCompiler.Compile();
                    Graph<int, IDependentActivity<int, int>, IEvent<int>> arrowGraph = arrowGraphCompiler.ToGraph();

                    if (arrowGraph == null)
                    {
                        throw new InvalidOperationException("Cannot construct arrow graph");
                    }
                    ArrowGraph = m_Mapper.Map<Graph<int, IDependentActivity<int, int>, IEvent<int>>, ArrowGraphModel>(arrowGraph);
                }
                GenerateArrowGraphDataFrom();
            }
        }

        private async Task GenerateArrowGraphDataFromAsync()
        {
            await Task.Run(() => GenerateArrowGraphDataFrom()).ConfigureAwait(true);
        }

        private void GenerateArrowGraphDataFrom()
        {
            lock (m_Lock)
            {
                ArrowGraphData = GenerateArrowGraphData(ArrowGraph);
                DecorateArrowGraph();
            }
            PublishArrowGraphDataUpdatedPayload();
        }

        private static ArrowGraphData GenerateArrowGraphData(ArrowGraphModel arrowGraph)
        {
            if (arrowGraph == null
                || arrowGraph.Nodes == null
                || !arrowGraph.Nodes.Any()
                || arrowGraph.Edges == null
                || !arrowGraph.Edges.Any())
            {
                return null;
            }
            IList<EventNodeModel> nodes = arrowGraph.Nodes.ToList();
            var edgeHeadVertexLookup = new Dictionary<int, ArrowGraphVertex>();
            var edgeTailVertexLookup = new Dictionary<int, ArrowGraphVertex>();
            var arrowGraphVertices = new List<ArrowGraphVertex>();
            foreach (EventNodeModel node in nodes)
            {
                var vertex = new ArrowGraphVertex(node.Content, node.NodeType);
                arrowGraphVertices.Add(vertex);
                foreach (int edgeId in node.IncomingEdges)
                {
                    edgeHeadVertexLookup.Add(edgeId, vertex);
                }
                foreach (int edgeId in node.OutgoingEdges)
                {
                    edgeTailVertexLookup.Add(edgeId, vertex);
                }
            }

            // Check all edges are used.
            IList<ActivityEdgeModel> edges = arrowGraph.Edges.ToList();
            IList<int> edgeIds = edges.Select(x => x.Content.Id).ToList();
            if (!edgeIds.OrderBy(x => x).SequenceEqual(edgeHeadVertexLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException("List of Edge IDs and Edges referenced by head Nodes do not match");
            }
            if (!edgeIds.OrderBy(x => x).SequenceEqual(edgeTailVertexLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException("List of Edge IDs and Edges referenced by tail Nodes do not match");
            }

            // Check all events are used.
            IEnumerable<long> edgeVertexLookupIds =
                edgeHeadVertexLookup.Values.Select(x => x.ID)
                .Union(edgeTailVertexLookup.Values.Select(x => x.ID));
            if (!arrowGraphVertices.Select(x => x.ID).OrderBy(x => x).SequenceEqual(edgeVertexLookupIds.OrderBy(x => x)))
            {
                throw new ArgumentException("List of Node IDs and Edges referenced by tail Nodes do not match");
            }

            // Check Start and End nodes.
            IEnumerable<EventNodeModel> startNodes = nodes.Where(x => x.NodeType == NodeType.Start);
            if (startNodes.Count() != 1)
            {
                throw new ArgumentException("Data contain more than one Start node");
            }
            IEnumerable<EventNodeModel> endNodes = nodes.Where(x => x.NodeType == NodeType.End);
            if (endNodes.Count() != 1)
            {
                throw new ArgumentException("Data contain more than one End node");
            }

            // Build the graph data.
            var graph = new ArrowGraphData();
            foreach (ArrowGraphVertex vertex in arrowGraphVertices)
            {
                graph.AddVertex(vertex);
            }
            foreach (ActivityEdgeModel activityEdge in edges)
            {
                ActivityModel activity = activityEdge.Content;
                var edge = new ArrowGraphEdge(
                    activity,
                    edgeTailVertexLookup[activity.Id],
                    edgeHeadVertexLookup[activity.Id]);
                graph.AddEdge(edge);
            }
            return graph;
        }

        private void DecorateArrowGraph()
        {
            lock (m_Lock)
            {
                DecorateArrowGraphByGraphSettings(ArrowGraphData, ArrowGraphSettings);
            }
        }

        private static void DecorateArrowGraphByGraphSettings(
            ArrowGraphData arrowGraphData,
            ArrowGraphSettingsModel arrowGraphSettings)
        {
            if (arrowGraphData == null)
            {
                return;
            }
            if (arrowGraphSettings == null)
            {
                throw new ArgumentNullException(nameof(arrowGraphSettings));
            }
            (GraphXEdgeFormatLookup edgeFormatLookup, SlackColorFormatLookup colorFormatLookup) = GetEdgeFormatLookups(arrowGraphSettings);
            foreach (ArrowGraphEdge edge in arrowGraphData.Edges)
            {
                edge.ForegroundHexCode = colorFormatLookup.FindSlackColorHexCode(edge.TotalSlack);
                edge.StrokeThickness = edgeFormatLookup.FindStrokeThickness(edge.IsCritical, edge.IsDummy);
                edge.DashStyle = edgeFormatLookup.FindGraphXEdgeDashStyle(edge.IsCritical, edge.IsDummy);
            }
        }

        private static (GraphXEdgeFormatLookup, SlackColorFormatLookup) GetEdgeFormatLookups(ArrowGraphSettingsModel arrowGraphSettings)
        {
            if (arrowGraphSettings == null)
            {
                throw new ArgumentNullException(nameof(arrowGraphSettings));
            }
            return (new GraphXEdgeFormatLookup(arrowGraphSettings.EdgeTypeFormats),
                new SlackColorFormatLookup(arrowGraphSettings.ActivitySeverities));
        }

        private void DispatchNotification(string title, object content)
        {
            m_NotificationInteractionRequest.Raise(
                new Notification
                {
                    Title = title,
                    Content = content
                });
        }

        #endregion

        #region Public Methods

        public async Task DoGenerateArrowGraphAsync()
        {
            try
            {
                IsBusy = true;
                await GenerateArrowGraphFromGraphCompilationAsync().ConfigureAwait(true);
                HasStaleArrowGraph = false;
                IsProjectUpdated = true;
            }
            catch (Exception ex)
            {
                DispatchNotification(
                    Resource.ProjectPlan.Resources.Title_Error,
                    ex.Message);
            }
            finally
            {
                IsBusy = false;
                RaiseCanExecuteChangedAllCommands();
            }
        }

        #endregion

        #region IArrowGraphManagerViewModel Members

        public string Title => Resource.ProjectPlan.Resources.Label_ArrowGraphViewTitle;

        public IInteractionRequest NotificationInteractionRequest => m_NotificationInteractionRequest;

        public bool IsBusy
        {
            get
            {
                return m_CoreViewModel.IsBusy;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.IsBusy = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool HasStaleArrowGraph
        {
            get
            {
                lock (m_Lock)
                {
                    if (HasStaleOutputs
                        && ArrowGraph != null)
                    {
                        ArrowGraph.IsStale = true;
                    }
                    return ArrowGraph?.IsStale ?? false;
                }
            }
            private set
            {
                lock (m_Lock)
                {
                    if (ArrowGraph != null)
                    {
                        ArrowGraph.IsStale = value;
                    }
                }
                RaisePropertyChanged();
            }
        }

        public ArrowGraphData ArrowGraphData
        {
            get
            {
                return m_ArrowGraphData;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_ArrowGraphData = value;
                }
                RaisePropertyChanged();
            }
        }

        public ICommand GenerateArrowGraphCommand
        {
            get;
            private set;
        }

        public byte[] ExportArrowGraphToDiagram(DiagramArrowGraphModel diagramArrowGraph)
        {
            if (diagramArrowGraph == null)
            {
                throw new ArgumentNullException(nameof(diagramArrowGraph));
            }
            return m_ProjectService.ExportArrowGraphToDiagram(diagramArrowGraph);
        }

        #endregion

        #region IActiveAware Members

        public event EventHandler IsActiveChanged;

        public bool IsActive
        {
            get
            {
                return m_IsActive;
            }
            set
            {
                if (m_IsActive != value)
                {
                    m_IsActive = value;
                    IsActiveChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        #endregion
    }
}
