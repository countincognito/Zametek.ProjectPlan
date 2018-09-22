using Prism.Commands;
using Prism.Events;
using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ArrowGraphManagerViewModel
        : PropertyChangedPubSubViewModel, IArrowGraphManagerViewModel
    {
        #region Fields

        private readonly object m_Lock;

        private ArrowGraphData m_ArrowGraphData;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IProjectManager m_ProjectManager;
        private readonly IEventAggregator m_EventService;

        private readonly InteractionRequest<Notification> m_NotificationInteractionRequest;

        private SubscriptionToken m_GraphCompiledPayloadToken;
        private SubscriptionToken m_ArrowGraphSettingsUpdatedPayloadToken;
        private SubscriptionToken m_ArrowGraphDtoUpdatedSubscriptionToken;

        #endregion

        #region Ctors

        public ArrowGraphManagerViewModel(
            ICoreViewModel coreViewModel,
            IProjectManager projectManager,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_ProjectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            m_NotificationInteractionRequest = new InteractionRequest<Notification>();

            InitializeCommands();
            SubscribeToEvents();

            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.IsBusy), nameof(IsBusy), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.HasStaleOutputs), nameof(HasStaleArrowGraph), ThreadOption.BackgroundThread);
        }

        #endregion

        #region Properties

        private ArrowGraphSettingsDto ArrowGraphSettingsDto => m_CoreViewModel.ArrowGraphSettingsDto;

        private bool HasStaleOutputs => m_CoreViewModel.HasStaleOutputs;

        private ArrowGraphDto ArrowGraphDto
        {
            get
            {
                return m_CoreViewModel.ArrowGraphDto;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.ArrowGraphDto = value;
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

        private GraphCompilation<int, IDependentActivity<int>> GraphCompilation => m_CoreViewModel.GraphCompilation;

        #endregion

        #region Commands

        private DelegateCommandBase InternalGenerateArrowGraphCommand
        {
            get;
            set;
        }

        private async void GenerateArrowGraph()
        {
            await DoGenerateArrowGraphAsync();
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
            m_GraphCompiledPayloadToken =
                m_EventService.GetEvent<PubSubEvent<GraphCompiledPayload>>()
                    .Subscribe(payload =>
                    {
                        HasStaleArrowGraph = true;
                    }, ThreadOption.BackgroundThread);
            m_ArrowGraphSettingsUpdatedPayloadToken =
                m_EventService.GetEvent<PubSubEvent<ArrowGraphSettingsUpdatedPayload>>()
                    .Subscribe(payload =>
                    {
                        HasStaleArrowGraph = true;
                    }, ThreadOption.BackgroundThread);
            m_ArrowGraphDtoUpdatedSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<ArrowGraphDtoUpdatedPayload>>()
                    .Subscribe(async payload =>
                    {
                        await GenerateArrowGraphDataFromDtoAsync();
                    }, ThreadOption.BackgroundThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompiledPayload>>()
                .Unsubscribe(m_GraphCompiledPayloadToken);
            m_EventService.GetEvent<PubSubEvent<ArrowGraphSettingsUpdatedPayload>>()
                .Unsubscribe(m_ArrowGraphSettingsUpdatedPayloadToken);
            m_EventService.GetEvent<PubSubEvent<ArrowGraphDtoUpdatedPayload>>()
                .Unsubscribe(m_ArrowGraphDtoUpdatedSubscriptionToken);
        }

        private void PublishArrowGraphDataUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<ArrowGraphDataUpdatedPayload>>()
                .Publish(new ArrowGraphDataUpdatedPayload());
        }

        private async Task GenerateArrowGraphFromGraphCompilationAsync()
        {
            await Task.Run(() => GenerateArrowGraphFromGraphCompilation());
        }

        private void GenerateArrowGraphFromGraphCompilation()
        {
            lock (m_Lock)
            {
                ArrowGraphDto = null;
                IList<IDependentActivity<int>> dependentActivities =
                    GraphCompilation.DependentActivities
                    .Select(x => (IDependentActivity<int>)x.WorkingCopy())
                    .ToList();

                if (!HasCompilationErrors
                    && dependentActivities.Any())
                {
                    var arrowGraphCompiler = ArrowGraphCompiler<int, IDependentActivity<int>>.Create();
                    foreach (DependentActivity<int> dependentActivity in dependentActivities)
                    {
                        dependentActivity.Dependencies.UnionWith(dependentActivity.ResourceDependencies);
                        dependentActivity.ResourceDependencies.Clear();
                        arrowGraphCompiler.AddActivity(dependentActivity);
                    }
                    
                    arrowGraphCompiler.Compile();
                    Graph<int, IDependentActivity<int>, IEvent<int>> arrowGraph = arrowGraphCompiler.ToGraph();

                    if (arrowGraph == null)
                    {
                        throw new InvalidOperationException("Cannot construct arrow graph");
                    }
                    ArrowGraphDto = DtoConverter.ToDto(arrowGraph);
                }
                GenerateArrowGraphDataFromDto();
            }
        }

        private async Task GenerateArrowGraphDataFromDtoAsync()
        {
            await Task.Run(() => GenerateArrowGraphDataFromDto());
        }

        private void GenerateArrowGraphDataFromDto()
        {
            lock (m_Lock)
            {
                ArrowGraphData = GenerateArrowGraphData(ArrowGraphDto);
                DecorateArrowGraph();
            }
            PublishArrowGraphDataUpdatedPayload();
        }

        private static ArrowGraphData GenerateArrowGraphData(ArrowGraphDto arrowGraph)
        {
            if (arrowGraph == null
                || arrowGraph.Nodes == null
                || !arrowGraph.Nodes.Any()
                || arrowGraph.Edges == null
                || !arrowGraph.Edges.Any())
            {
                return null;
            }
            IList<EventNodeDto> nodeDtos = arrowGraph.Nodes.ToList();
            var edgeHeadVertexLookup = new Dictionary<int, ArrowGraphVertex>();
            var edgeTailVertexLookup = new Dictionary<int, ArrowGraphVertex>();
            var arrowGraphVertices = new List<ArrowGraphVertex>();
            foreach (EventNodeDto nodeDto in nodeDtos)
            {
                var vertex = new ArrowGraphVertex(nodeDto.Content, nodeDto.NodeType);
                arrowGraphVertices.Add(vertex);
                foreach (int edgeId in nodeDto.IncomingEdges)
                {
                    edgeHeadVertexLookup.Add(edgeId, vertex);
                }
                foreach (int edgeId in nodeDto.OutgoingEdges)
                {
                    edgeTailVertexLookup.Add(edgeId, vertex);
                }
            }

            // Check all edges are used.
            IList<ActivityEdgeDto> edgeDtos = arrowGraph.Edges.ToList();
            IList<int> edgeIds = edgeDtos.Select(x => x.Content.Id).ToList();
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
            IEnumerable<EventNodeDto> startNodes = nodeDtos.Where(x => x.NodeType == NodeType.Start);
            if (startNodes.Count() != 1)
            {
                throw new ArgumentException("Data contain more than one Start node");
            }
            IEnumerable<EventNodeDto> endNodes = nodeDtos.Where(x => x.NodeType == NodeType.End);
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
            foreach (ActivityEdgeDto edgeDto in edgeDtos)
            {
                ActivityDto activityDto = edgeDto.Content;
                var edge = new ArrowGraphEdge(
                    activityDto,
                    edgeTailVertexLookup[activityDto.Id],
                    edgeHeadVertexLookup[activityDto.Id]);
                graph.AddEdge(edge);
            }
            return graph;
        }

        private void DecorateArrowGraph()
        {
            lock (m_Lock)
            {
                DecorateArrowGraphByGraphSettings(ArrowGraphData, ArrowGraphSettingsDto);
            }
        }

        private static void DecorateArrowGraphByGraphSettings(ArrowGraphData arrowGraphData, ArrowGraphSettingsDto arrowGraphSettings)
        {
            if (arrowGraphData == null)
            {
                return;
            }
            if (arrowGraphSettings == null)
            {
                throw new ArgumentNullException(nameof(arrowGraphSettings));
            }
            GraphXEdgeFormatLookup edgeFormatLookup = GetEdgeFormatLookup(arrowGraphSettings);
            foreach (ArrowGraphEdge edge in arrowGraphData.Edges)
            {
                edge.ForegroundHexCode = edgeFormatLookup.FindSlackColorHexCode(edge.TotalSlack);
                edge.StrokeThickness = edgeFormatLookup.FindStrokeThickness(edge.IsCritical, edge.IsDummy);
                edge.DashStyle = edgeFormatLookup.FindDashStyle(edge.IsCritical, edge.IsDummy);
            }
        }

        private static GraphXEdgeFormatLookup GetEdgeFormatLookup(ArrowGraphSettingsDto arrowGraphSettingsDto)
        {
            if (arrowGraphSettingsDto == null)
            {
                throw new ArgumentNullException(nameof(arrowGraphSettingsDto));
            }
            return new GraphXEdgeFormatLookup(arrowGraphSettingsDto.ActivitySeverities, arrowGraphSettingsDto.EdgeTypeFormats);
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
                await GenerateArrowGraphFromGraphCompilationAsync();
                HasStaleArrowGraph = false;
                IsProjectUpdated = true;
            }
            catch (Exception ex)
            {
                DispatchNotification(
                    Properties.Resources.Title_Error,
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
                        && ArrowGraphDto != null)
                    {
                        ArrowGraphDto.IsStale = true;
                    }
                    return ArrowGraphDto?.IsStale ?? false;
                }
            }
            private set
            {
                lock (m_Lock)
                {
                    if (ArrowGraphDto != null)
                    {
                        ArrowGraphDto.IsStale = value;
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

        public byte[] ExportArrowGraphToDiagram(DiagramArrowGraphDto diagramArrowGraphDto)
        {
            if (diagramArrowGraphDto == null)
            {
                throw new ArgumentNullException(nameof(diagramArrowGraphDto));
            }
            return m_ProjectManager.ExportArrowGraphToDiagram(diagramArrowGraphDto);
        }

        #endregion
    }
}
