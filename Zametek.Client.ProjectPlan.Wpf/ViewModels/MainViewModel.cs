using FluentDateTime;
using net.sf.mpxj.MpxjUtilities;
using net.sf.mpxj.reader;
using Prism.Commands;
using Prism.Events;
using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class MainViewModel
        : PropertyChangedPubSubViewModel, IMainViewModel, IActivitiesManagerViewModel, IArrowGraphManagerViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private readonly VertexGraphCompiler<int, IDependentActivity<int>> m_VertexGraphCompiler;
        private string m_ProjectTitle;
        private bool m_IsProjectUpdated;
        private bool m_AutoCompile;
        private bool m_IsBusy;
        private string m_CompilationOutput;
        private bool m_HasStaleArrowGraph;

        private static string s_DefaultProjectTitle = Properties.Resources.Label_DefaultTitle;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IProjectManager m_ProjectManager;
        private readonly ISettingManager m_SettingManager;
        private readonly IFileDialogService m_FileDialogService;
        private readonly IAppSettingService m_AppSettingService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IEventAggregator m_EventService;
        private readonly InteractionRequest<Notification> m_NotificationInteractionRequest;
        private readonly InteractionRequest<Confirmation> m_ConfirmationInteractionRequest;
        private readonly InteractionRequest<Confirmation> m_ProjectTitleInteractionRequest;
        private readonly InteractionRequest<ResourceSettingsManagerConfirmation> m_ResourceSettingsManagerInteractionRequest;
        private readonly InteractionRequest<ArrowGraphSettingsManagerConfirmation> m_ArrowGraphSettingsManagerInteractionRequest;
        private SubscriptionToken m_ManagedActivityUpdatedPayloadToken;
        private SubscriptionToken m_ProjectStartUpdatedPayloadToken;
        private SubscriptionToken m_UseBusinessDaysUpdatedPayloadToken;

        #endregion

        #region Ctors

        public MainViewModel(
            ICoreViewModel coreViewModel,
            IProjectManager projectManager,
            ISettingManager settingManager,
            IFileDialogService fileDialogService,
            IAppSettingService appSettingService,
            IDateTimeCalculator dateTimeCalculator,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_ProjectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
            m_SettingManager = settingManager ?? throw new ArgumentNullException(nameof(settingManager));
            m_FileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            m_AppSettingService = appSettingService ?? throw new ArgumentNullException(nameof(appSettingService));
            m_DateTimeCalculator = dateTimeCalculator ?? throw new ArgumentNullException(nameof(dateTimeCalculator));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            m_VertexGraphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            m_NotificationInteractionRequest = new InteractionRequest<Notification>();
            m_ConfirmationInteractionRequest = new InteractionRequest<Confirmation>();
            m_ProjectTitleInteractionRequest = new InteractionRequest<Confirmation>();
            m_ResourceSettingsManagerInteractionRequest = new InteractionRequest<ResourceSettingsManagerConfirmation>();
            m_ArrowGraphSettingsManagerInteractionRequest = new InteractionRequest<ArrowGraphSettingsManagerConfirmation>();
            Activities = new ObservableCollection<ManagedActivityViewModel>();
            SelectedActivities = new ObservableCollection<ManagedActivityViewModel>();

            ResetProject();

            ShowDates = false;
            UseBusinessDaysWithoutPublishing = true;
            AutoCompile = true;
            InitializeCommands();
            SubscribeToEvents();

            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.ProjectStart), nameof(ProjectStart), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.ShowDates), nameof(ShowDates), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.ShowDates), nameof(ShowDays), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.UseBusinessDays), nameof(UseBusinessDays), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.HasStaleOutputs), nameof(HasStaleOutputs), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.HasCompilationErrors), nameof(HasCompilationErrors), ThreadOption.BackgroundThread);
        }

        #endregion

        #region Properties

        public bool HasStaleOutputs
        {
            get
            {
                return m_CoreViewModel.HasStaleOutputs;
            }
            private set
            {
                m_CoreViewModel.HasStaleOutputs = value;
                if (m_CoreViewModel.HasStaleOutputs
                    && ArrowGraphDto != null)
                {
                    HasStaleArrowGraph = true;
                }
                RaisePropertyChanged(nameof(HasStaleOutputs));
            }
        }

        public DateTime ProjectStartWithoutPublishing
        {
            get
            {
                return m_CoreViewModel.ProjectStart;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.ProjectStart = value;
                }
                IsProjectUpdated = true;
                RaisePropertyChanged(nameof(ProjectStart));
            }
        }

        public bool UseBusinessDaysWithoutPublishing
        {
            get
            {
                return m_CoreViewModel.UseBusinessDays;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.UseBusinessDays = value;
                    m_DateTimeCalculator.UseBusinessDays(value);
                }
                RaisePropertyChanged(nameof(UseBusinessDays));
            }
        }

        public bool IsBusy
        {
            get
            {
                return m_IsBusy;
            }
            set
            {
                m_IsBusy = value;
                RaisePropertyChanged(nameof(IsBusy));
            }
        }

        public GraphCompilation<int, IDependentActivity<int>> GraphCompilation
        {
            get
            {
                return m_CoreViewModel.GraphCompilation;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.GraphCompilation = value;
                }
            }
        }

        public string CompilationOutput
        {
            get
            {
                return m_CompilationOutput;
            }
            private set
            {
                m_CompilationOutput = value;
                RaisePropertyChanged(nameof(CompilationOutput));
            }
        }

        public bool HasCompilationErrors
        {
            get
            {
                return m_CoreViewModel.HasCompilationErrors;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.HasCompilationErrors = value;
                }
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<ManagedActivityViewModel> Activities
        {
            get;
        }

        public ObservableCollection<ManagedActivityViewModel> SelectedActivities
        {
            get;
        }

        public ManagedActivityViewModel SelectedActivity
        {
            get
            {
                if (SelectedActivities.Count == 1)
                {
                    return SelectedActivities.FirstOrDefault();
                }
                return null;
            }
        }

        public bool DisableResources
        {
            get;
            set;
        }

        public IList<ResourceDto> ResourceDtos => m_CoreViewModel.ResourceDtos;

        public IInteractionRequest NotificationInteractionRequest => m_NotificationInteractionRequest;

        public IInteractionRequest ConfirmationInteractionRequest => m_ConfirmationInteractionRequest;

        public IInteractionRequest ProjectTitleInteractionRequest => m_ProjectTitleInteractionRequest;

        public IInteractionRequest ResourceSettingsManagerInteractionRequest => m_ResourceSettingsManagerInteractionRequest;

        public IInteractionRequest ArrowGraphSettingsManagerInteractionRequest => m_ArrowGraphSettingsManagerInteractionRequest;

        private int? CyclomaticComplexity
        {
            get
            {
                return m_CoreViewModel.CyclomaticComplexity;
            }
            set
            {
                m_CoreViewModel.CyclomaticComplexity = value;
            }
        }

        private int? Duration
        {
            get
            {
                return m_CoreViewModel.Duration;
            }
            set
            {
                m_CoreViewModel.Duration = value;
            }
        }

        #endregion

        #region Commands

        private DelegateCommandBase InternalOpenProjectPlanFileCommand
        {
            get;
            set;
        }

        private async void OpenProjectPlanFile()
        {
            await DoOpenProjectPlanFileAsync();
        }

        private bool CanOpenProjectPlanFile()
        {
            return true;
        }

        private DelegateCommandBase InternalSaveProjectPlanFileCommand
        {
            get;
            set;
        }

        private async void SaveProjectPlanFile()
        {
            await DoSaveProjectPlanFileAsync();
        }

        private bool CanSaveProjectPlanFile()
        {
            return true;
        }

        private DelegateCommandBase InternalImportMicrosoftProjectCommand
        {
            get;
            set;
        }

        private async void ImportMicrosoftProject()
        {
            await DoImportMicrosoftProjectAsync();
        }

        private bool CanImportMicrosoftProject()
        {
            return true;
        }

        private DelegateCommandBase InternalCloseProjectCommand
        {
            get;
            set;
        }

        private void CloseProject()
        {
            DoCloseProject();
        }

        private bool CanCloseProject()
        {
            return true;
        }

        private DelegateCommandBase InternalOpenResourceSettingsCommand
        {
            get;
            set;
        }

        private async void OpenResourceSettings()
        {
            await DoOpenResourceSettingsAsync();
        }

        private bool CanOpenResourceSettings()
        {
            return true;
        }

        private DelegateCommandBase InternalOpenArrowGraphSettingsCommand
        {
            get;
            set;
        }

        private void OpenArrowGraphSettings()
        {
            DoOpenArrowGraphSettings();
        }

        private bool CanOpenArrowGraphSettings()
        {
            return true;
        }

        private DelegateCommandBase InternalCompileCommand
        {
            get;
            set;
        }

        private async void Compile()
        {
            await DoCompileAsync();
        }

        private bool CanCompile()
        {
            return !IsBusy;
        }

        public DelegateCommandBase SetSelectedManagedActivitiesCommand
        {
            get;
            private set;
        }

        private void SetSelectedManagedActivities(SelectionChangedEventArgs args)
        {
            if (args?.AddedItems != null)
            {
                SelectedActivities.AddRange(args?.AddedItems.OfType<ManagedActivityViewModel>());
            }
            if (args?.RemovedItems != null)
            {
                foreach (var managedActivityViewModel in args?.RemovedItems.OfType<ManagedActivityViewModel>())
                {
                    SelectedActivities.Remove(managedActivityViewModel);
                }
            }
            RaisePropertyChanged(nameof(SelectedActivity));
            RaiseCanExecuteChangedAllCommands();
        }

        private DelegateCommandBase InternalAddManagedActivityCommand
        {
            get;
            set;
        }

        private async void AddManagedActivity()
        {
            await DoAddManagedActivityAsync();
        }

        private bool CanAddManagedActivity()
        {
            return true;
        }

        private DelegateCommandBase InternalRemoveManagedActivityCommand
        {
            get;
            set;
        }

        private async void RemoveManagedActivity()
        {
            await DoRemoveManagedActivityAsync();
        }

        private bool CanRemoveManagedActivity()
        {
            return SelectedActivities.Any();
        }

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
            OpenProjectPlanFileCommand =
                InternalOpenProjectPlanFileCommand =
                    new DelegateCommand(OpenProjectPlanFile, CanOpenProjectPlanFile);
            SaveProjectPlanFileCommand =
                InternalSaveProjectPlanFileCommand =
                    new DelegateCommand(SaveProjectPlanFile, CanSaveProjectPlanFile);
            ImportMicrosoftProjectCommand =
                InternalImportMicrosoftProjectCommand =
                    new DelegateCommand(ImportMicrosoftProject, CanImportMicrosoftProject);
            CloseProjectCommand =
                InternalCloseProjectCommand =
                    new DelegateCommand(CloseProject, CanCloseProject);
            OpenResourceSettingsCommand =
                InternalOpenResourceSettingsCommand =
                    new DelegateCommand(OpenResourceSettings, CanOpenResourceSettings);
            OpenArrowGraphSettingsCommand =
                InternalOpenArrowGraphSettingsCommand =
                    new DelegateCommand(OpenArrowGraphSettings, CanOpenArrowGraphSettings);
            CompileCommand =
                InternalCompileCommand =
                    new DelegateCommand(Compile, CanCompile);
            SetSelectedManagedActivitiesCommand =
                new DelegateCommand<SelectionChangedEventArgs>(SetSelectedManagedActivities);
            AddManagedActivityCommand =
                InternalAddManagedActivityCommand =
                    new DelegateCommand(AddManagedActivity, CanAddManagedActivity);
            RemoveManagedActivityCommand =
                InternalRemoveManagedActivityCommand =
                    new DelegateCommand(RemoveManagedActivity, CanRemoveManagedActivity);
            GenerateArrowGraphCommand =
                InternalGenerateArrowGraphCommand =
                    new DelegateCommand(GenerateArrowGraph, CanGenerateArrowGraph);
        }

        private void RaiseCanExecuteChangedAllCommands()
        {
            InternalOpenProjectPlanFileCommand.RaiseCanExecuteChanged();
            InternalSaveProjectPlanFileCommand.RaiseCanExecuteChanged();
            InternalImportMicrosoftProjectCommand.RaiseCanExecuteChanged();
            InternalCloseProjectCommand.RaiseCanExecuteChanged();
            InternalOpenResourceSettingsCommand.RaiseCanExecuteChanged();
            InternalOpenArrowGraphSettingsCommand.RaiseCanExecuteChanged();
            SetSelectedManagedActivitiesCommand.RaiseCanExecuteChanged();
            InternalAddManagedActivityCommand.RaiseCanExecuteChanged();
            InternalRemoveManagedActivityCommand.RaiseCanExecuteChanged();
            InternalGenerateArrowGraphCommand.RaiseCanExecuteChanged();
        }

        private void SubscribeToEvents()
        {
            m_ManagedActivityUpdatedPayloadToken =
                m_EventService.GetEvent<PubSubEvent<ManagedActivityUpdatedPayload>>()
                    .Subscribe(async payload =>
                    {
                        IsProjectUpdated = true;
                        await UpdateActivitiesTargetResourceDependenciesAsync();
                        await DoAutoCompileAsync();
                    }, ThreadOption.BackgroundThread);
            m_ProjectStartUpdatedPayloadToken =
                m_EventService.GetEvent<PubSubEvent<ProjectStartUpdatedPayload>>()
                    .Subscribe(async payload =>
                    {
                        IsProjectUpdated = true;
                        await UpdateActivitiesProjectStartAsync();
                        await DoAutoCompileAsync();
                    }, ThreadOption.BackgroundThread);
            m_UseBusinessDaysUpdatedPayloadToken =
                m_EventService.GetEvent<PubSubEvent<UseBusinessDaysUpdatedPayload>>()
                    .Subscribe(async payload =>
                    {
                        IsProjectUpdated = true;
                        await UpdateActivitiesUseBusinessDaysAsync();
                        await DoAutoCompileAsync();
                    }, ThreadOption.BackgroundThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<ManagedActivityUpdatedPayload>>()
                .Unsubscribe(m_ManagedActivityUpdatedPayloadToken);
            m_EventService.GetEvent<PubSubEvent<ProjectStartUpdatedPayload>>()
                .Unsubscribe(m_ProjectStartUpdatedPayloadToken);
            m_EventService.GetEvent<PubSubEvent<UseBusinessDaysUpdatedPayload>>()
                .Unsubscribe(m_UseBusinessDaysUpdatedPayloadToken);
        }

        private void PublishProjectStartUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<ProjectStartUpdatedPayload>>()
                .Publish(new ProjectStartUpdatedPayload(ProjectStart));
        }

        private void PublishUseBusinessDaysUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<UseBusinessDaysUpdatedPayload>>()
                .Publish(new UseBusinessDaysUpdatedPayload(UseBusinessDays));
        }

        private void PublishGraphCompiledPayload()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompiledPayload>>()
                .Publish(new GraphCompiledPayload());
        }

        private void SetActivitiesTargetResources()
        {
            lock (m_Lock)
            {
                foreach (ManagedActivityViewModel activity in Activities)
                {
                    activity.SetTargetResources(ResourceDtos.Select(x => x.Copy()));
                }
            }
        }

        private async Task UpdateActivitiesTargetResourceDependenciesAsync()
        {
            await Task.Run(() => UpdateActivitiesTargetResourceDependencies());
        }

        private void UpdateActivitiesTargetResourceDependencies()
        {
            lock (m_Lock)
            {
                foreach (ManagedActivityViewModel activity in Activities.Where(x => x.HasUpdatedDependencies))
                {
                    m_VertexGraphCompiler.SetActivityDependencies(activity.Id, new HashSet<int>(activity.UpdatedDependencies));
                    activity.UpdatedDependencies.Clear();
                    activity.HasUpdatedDependencies = false;
                }
            }
        }

        private async Task UpdateActivitiesProjectStartAsync()
        {
            await Task.Run(() => UpdateActivitiesProjectStart());
        }

        private void UpdateActivitiesProjectStart()
        {
            lock (m_Lock)
            {
                foreach (ManagedActivityViewModel activity in Activities)
                {
                    activity.ProjectStart = ProjectStart;
                }
            }
        }

        private async Task UpdateActivitiesUseBusinessDaysAsync()
        {
            await Task.Run(() => UpdateActivitiesUseBusinessDays());
        }

        private void UpdateActivitiesUseBusinessDays()
        {
            lock (m_Lock)
            {
                foreach (ManagedActivityViewModel activity in Activities)
                {
                    activity.UseBusinessDays = UseBusinessDays;
                }
            }
        }

        private async Task RunCompileAsync()
        {
            await Task.Run(() => RunCompile());
        }

        private void RunCompile()
        {
            lock (m_Lock)
            {
                UpdateActivitiesTargetResourceDependencies();

                var availableResources = new List<IResource<int>>();
                if (!DisableResources)
                {
                    availableResources.AddRange(ResourceDtos.Select(x => DtoConverter.FromDto(x)));
                }

                GraphCompilation = m_VertexGraphCompiler.Compile(availableResources);
                CyclomaticComplexity = m_VertexGraphCompiler.CyclomaticComplexity;
                Duration = m_VertexGraphCompiler.Duration;
                IsProjectUpdated = true;

                if (ArrowGraphDto != null)
                {
                    HasStaleArrowGraph = true;
                }
                SetCompilationOutput();
                HasStaleOutputs = false;
            }
        }

        private async Task SetCompilationOutputAsync()
        {
            await Task.Run(() => SetCompilationOutput());
        }

        private void SetCompilationOutput()
        {
            lock (m_Lock)
            {
                GraphCompilation<int, IDependentActivity<int>> graphCompilation = GraphCompilation;
                CompilationOutput = string.Empty;
                HasCompilationErrors = false;
                if (graphCompilation == null)
                {
                    return;
                }
                var output = new StringBuilder();

                if (graphCompilation.AllResourcesExplicitTargetsButNotAllActivitiesTargeted)
                {
                    HasCompilationErrors = true;
                    output.AppendLine($@">{Properties.Resources.Message_AllResourcesExplicitTargetsNotAllActivitiesTargeted}");
                }

                if (graphCompilation.CircularDependencies.Any())
                {
                    HasCompilationErrors = true;
                    output.Append(BuildCircularDependenciesErrorMessage(graphCompilation.CircularDependencies));
                }

                if (graphCompilation.MissingDependencies.Any())
                {
                    HasCompilationErrors = true;
                    output.Append(BuildMissingDependenciesErrorMessage(graphCompilation.MissingDependencies));
                }

                if (graphCompilation.ResourceSchedules.Any()
                    && !HasCompilationErrors)
                {
                    output.Append(BuildActivitySchedules(graphCompilation.ResourceSchedules));
                }

                if (HasCompilationErrors)
                {
                    output.Insert(0, Environment.NewLine);
                    output.Insert(0, $@">{Properties.Resources.Message_CompilationErrors}");
                }

                CompilationOutput = output.ToString();
            }
            PublishGraphCompiledPayload();
        }

        private string BuildCircularDependenciesErrorMessage(IList<CircularDependency<int>> circularDependencies)
        {
            if (circularDependencies == null)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@">{Properties.Resources.Message_CircularDependencies}");
            foreach (CircularDependency<int> circularDependency in circularDependencies)
            {
                output.AppendLine(string.Join(@" -> ", circularDependency.Dependencies));
            }
            return output.ToString();
        }

        private string BuildMissingDependenciesErrorMessage(IList<int> missingDependencies)
        {
            if (missingDependencies == null)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@">{Properties.Resources.Message_MissingDependencies}");
            foreach (int missingDependency in missingDependencies)
            {
                IList<int> activities = Activities
                    .Where(x => x.Dependencies.Contains(missingDependency))
                    .Select(x => x.Id)
                    .ToList();
                output.AppendFormat($@"{missingDependency} -> ");
                output.AppendLine(string.Join(@", ", activities));
            }
            return output.ToString();
        }

        private string BuildActivitySchedules(IList<IResourceSchedule<int>> resourceSchedules)
        {
            lock (m_Lock)
            {
                if (resourceSchedules == null)
                {
                    return string.Empty;
                }
                var output = new StringBuilder();
                int spareResourceCount = 1;
                for (int resourceIndex = 0; resourceIndex < resourceSchedules.Count; resourceIndex++)
                {
                    IResourceSchedule<int> resourceSchedule = resourceSchedules[resourceIndex];
                    IList<IScheduledActivity<int>> scheduledActivities = resourceSchedule?.ScheduledActivities;
                    if (scheduledActivities == null)
                    {
                        continue;
                    }
                    var stringBuilder = new StringBuilder(@">Resource");
                    if (resourceSchedule.Resource != null)
                    {
                        stringBuilder.Append($@" {resourceSchedule.Resource.Id}");
                        if (!string.IsNullOrWhiteSpace(resourceSchedule.Resource.Name))
                        {
                            stringBuilder.Append($@" ({resourceSchedule.Resource.Name})");
                        }
                    }
                    else
                    {
                        stringBuilder.Append($@" {spareResourceCount}");
                        spareResourceCount++;
                    }
                    output.AppendLine(stringBuilder.ToString());
                    int previousFinishTime = 0;
                    foreach (IScheduledActivity<int> scheduledActivity in scheduledActivities)
                    {
                        int startTime = scheduledActivity.StartTime;
                        int finishTime = scheduledActivity.FinishTime;
                        if (startTime > previousFinishTime)
                        {
                            string from = ChartHelper.FormatScheduleOutput(previousFinishTime, ShowDates, ProjectStart, m_DateTimeCalculator);
                            string to = ChartHelper.FormatScheduleOutput(startTime, ShowDates, ProjectStart, m_DateTimeCalculator);
                            output.AppendLine($@"*** {from} -> {to} ***");
                        }
                        string start = ChartHelper.FormatScheduleOutput(startTime, ShowDates, ProjectStart, m_DateTimeCalculator);
                        string finish = ChartHelper.FormatScheduleOutput(finishTime, ShowDates, ProjectStart, m_DateTimeCalculator);
                        output.AppendLine($@"Activity {scheduledActivity.Id}: {start} -> {finish}");
                        previousFinishTime = finishTime;
                    }
                    output.AppendLine();
                }
                return output.ToString();
            }
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
                    ArrowGraphCompiler<int, IDependentActivity<int>> arrowGraphCompiler = ArrowGraphCompiler<int, IDependentActivity<int>>.Create();
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
                ArrowGraphData = GenerateArrowGraphData(ArrowGraphDto);
                DecorateArrowGraph();
                HasStaleArrowGraph = false;
            }
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

        private async Task<MicrosoftProjectDto> ImportMicrosoftProjectAsync(string filename)
        {
            return await Task.Run(() => ImportMicrosoftProject(filename));
        }

        private static MicrosoftProjectDto ImportMicrosoftProject(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }
            ProjectReader reader = ProjectReaderUtility.getProjectReader(filename);
            net.sf.mpxj.ProjectFile mpx = reader.read(filename);
            DateTime projectStart = mpx.ProjectProperties.StartDate.ToDateTime();

            var resources = new List<ResourceDto>();
            foreach (var resource in mpx.AllResources.ToIEnumerable<net.sf.mpxj.Resource>())
            {
                int id = resource.ID.intValue();
                if (id == 0)
                {
                    continue;
                }
                string name = resource.Name;
                var resourceDto = new ResourceDto
                {
                    Id = id,
                    IsExplicitTarget = true,
                    Name = name,
                    DisplayOrder = id,
                    ColorFormat = new ColorFormatDto()
                };
                resources.Add(resourceDto);
            }

            var dependentActivities = new List<DependentActivityDto>();
            foreach (var task in mpx.AllTasks.ToIEnumerable<net.sf.mpxj.Task>())
            {
                int id = task.ID.intValue();
                if (id == 0)
                {
                    continue;
                }
                string name = task.Name;
                int duration = Convert.ToInt32(task.Duration.Duration);

                DateTime? minimumEarliestStartDateTime = null;
                if (task.ConstraintType == net.sf.mpxj.ConstraintType.START_NO_EARLIER_THAN)
                {
                    //minimumEarliestStartTime = Convert.ToInt32((task.ConstraintDate.ToDateTime() - projectStart).TotalDays);
                    minimumEarliestStartDateTime = task.ConstraintDate.ToDateTime();
                }

                var targetResources = new List<int>();
                foreach (var resourceAssignment in task.ResourceAssignments.ToIEnumerable<net.sf.mpxj.ResourceAssignment>())
                {
                    if (resourceAssignment.Resource != null)
                    {
                        targetResources.Add(resourceAssignment.Resource.ID.intValue());
                    }
                }

                var dependencies = new List<int>();
                var preds = task.Predecessors;
                if (preds != null && !preds.isEmpty())
                {
                    foreach (var pred in preds.ToIEnumerable<net.sf.mpxj.Relation>())
                    {
                        dependencies.Add(pred.TargetTask.ID.intValue());
                    }
                }
                var dependentActivityDto = new DependentActivityDto
                {
                    Activity = new ActivityDto
                    {
                        Id = id,
                        Name = name,
                        TargetResources = targetResources,
                        Duration = duration,
                        MinimumEarliestStartDateTime = minimumEarliestStartDateTime
                    },
                    Dependencies = dependencies,
                    ResourceDependencies = new List<int>()
                };
                dependentActivities.Add(dependentActivityDto);
            }
            return new MicrosoftProjectDto
            {
                ProjectStart = projectStart,
                DependentActivities = dependentActivities.ToList(),
                Resources = resources.ToList()
            };
        }

        private void ProcessMicrosoftProject(MicrosoftProjectDto microsoftProjectDto)
        {
            if (microsoftProjectDto == null)
            {
                throw new ArgumentNullException(nameof(microsoftProjectDto));
            }
            lock (m_Lock)
            {
                ResetProject();

                // Project Start Date.
                ProjectStartWithoutPublishing = microsoftProjectDto.ProjectStart;

                // Resources.
                foreach (ResourceDto resourceDto in microsoftProjectDto.Resources)
                {
                    ResourceDtos.Add(resourceDto);
                }
                //SetTargetResources();

                // Activities.
                foreach (DependentActivityDto dependentActivityDto in microsoftProjectDto.DependentActivities)
                {
                    var dateTimeCalculator = new DateTimeCalculator();
                    dateTimeCalculator.UseBusinessDays(UseBusinessDays);
                    var activity = new ManagedActivityViewModel(
                        DtoConverter.FromDto(dependentActivityDto),
                        ProjectStart,
                        ResourceDtos,
                        dateTimeCalculator,
                        m_EventService);
                    if (m_VertexGraphCompiler.AddActivity(activity))
                    {
                        Activities.Add(activity);
                    }
                }
            }
        }

        private void ProcessProjectPlanDto(ProjectPlanDto projectPlanDto)
        {
            if (projectPlanDto == null)
            {
                throw new ArgumentNullException(nameof(projectPlanDto));
            }
            lock (m_Lock)
            {
                ResetProject();

                // Project Start Date.
                ProjectStartWithoutPublishing = projectPlanDto.ProjectStart;

                // Resources.
                DisableResources = projectPlanDto.DisableResources;
                foreach (ResourceDto resourceDto in projectPlanDto.Resources)
                {
                    ResourceDtos.Add(resourceDto);
                }

                // Arrow Graph.
                ArrowGraphSettingsDto = projectPlanDto.ArrowGraphSettings;
                ArrowGraphDto = projectPlanDto.ArrowGraph;
                ArrowGraphData = GenerateArrowGraphData(ArrowGraphDto);
                DecorateArrowGraph();

                // Activities.
                foreach (DependentActivityDto dependentActivityDto in projectPlanDto.DependentActivities)
                {
                    var activityId = m_VertexGraphCompiler.GetNextActivityId();
                    var dateTimeCalculator = new DateTimeCalculator();
                    dateTimeCalculator.UseBusinessDays(UseBusinessDays);
                    var activity = new ManagedActivityViewModel(
                        DtoConverter.FromDto(dependentActivityDto),
                        ProjectStart,
                        ResourceDtos,
                        dateTimeCalculator,
                        m_EventService);
                    if (m_VertexGraphCompiler.AddActivity(activity))
                    {
                        Activities.Add(activity);
                    }
                }

                CyclomaticComplexity = projectPlanDto.CyclomaticComplexity;
                Duration = projectPlanDto.Duration;

                // Compilation.
                GraphCompilation = new GraphCompilation<int, IDependentActivity<int>>(
                    projectPlanDto.AllResourcesExplicitTargetsButNotAllActivitiesTargeted,
                    projectPlanDto.CircularDependencies.Select(x => DtoConverter.FromDto(x)),
                    projectPlanDto.MissingDependencies,
                    projectPlanDto.DependentActivities.Select(x => DtoConverter.FromDto(x)),
                    projectPlanDto.ResourceSchedules.Select(x => DtoConverter.FromDto(x)));

                SetCompilationOutput();

                HasStaleArrowGraph = projectPlanDto.HasStaleArrowGraph;
                HasStaleOutputs = projectPlanDto.HasStaleOutputs;
            }
            m_EventService
                .GetEvent<PubSubEvent<ArrowGraphGeneratedPayload>>()
                .Publish(new ArrowGraphGeneratedPayload());
        }

        private async Task<ProjectPlanDto> BuildProjectPlanDtoAsync()
        {
            return await Task.Run(() => BuildProjectPlanDto());
        }

        private ProjectPlanDto BuildProjectPlanDto()
        {
            lock (m_Lock)
            {
                return new ProjectPlanDto()
                {
                    ProjectStart = ProjectStart,
                    Resources = ResourceDtos.Select(x => x.Copy()).ToList(),
                    DisableResources = DisableResources,
                    ArrowGraphSettings = ArrowGraphSettingsDto.Copy(),

                    AllResourcesExplicitTargetsButNotAllActivitiesTargeted = GraphCompilation?.AllResourcesExplicitTargetsButNotAllActivitiesTargeted != null ? GraphCompilation.AllResourcesExplicitTargetsButNotAllActivitiesTargeted : false,
                    CircularDependencies = GraphCompilation?.CircularDependencies != null ? GraphCompilation.CircularDependencies.Select(x => DtoConverter.ToDto(x)).ToList() : new List<CircularDependencyDto>(),
                    MissingDependencies = GraphCompilation?.MissingDependencies != null ? GraphCompilation.MissingDependencies.ToList() : new List<int>(),
                    DependentActivities = Activities.Select(x => DtoConverter.ToDto(x)).ToList(), //GraphCompilation?.DependentActivities != null ? GraphCompilation.DependentActivities.Select(x => DtoConverter.ToDto(x)).ToList() : new List<DependentActivityDto>(),
                    ResourceSchedules = GraphCompilation?.ResourceSchedules != null ? GraphCompilation.ResourceSchedules.Select(x => DtoConverter.ToDto(x)).ToList() : new List<ResourceScheduleDto>(),

                    CyclomaticComplexity = CyclomaticComplexity.GetValueOrDefault(),
                    Duration = Duration.GetValueOrDefault(),

                    ArrowGraph = ArrowGraphDto != null ? ArrowGraphDto.Copy() : new ArrowGraphDto() { Edges = new List<ActivityEdgeDto>(), Nodes = new List<EventNodeDto>() },
                    HasStaleArrowGraph = HasStaleArrowGraph,

                    HasStaleOutputs = HasStaleOutputs
                };
            }
        }

        private Task<ProjectPlanDto> OpenProjectPlanDtoAsync(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException(nameof(filename));
            }
            return Task.Run(() => OpenSave.OpenJson<ProjectPlanDto>(filename));
        }

        private static Task SaveProjectPlanDtoAsync(ProjectPlanDto projectPlanDto, string filename)
        {
            if (projectPlanDto == null)
            {
                throw new ArgumentNullException(nameof(projectPlanDto));
            }
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException(nameof(filename));
            }
            return Task.Run(() => OpenSave.SaveJson(projectPlanDto, filename));
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

        public async Task DoSaveProjectPlanFileAsync()
        {
            try
            {
                IsBusy = true;
                string directory = m_AppSettingService.ProjectPlanFolder;
                if (m_FileDialogService.ShowSaveDialog(
                    directory,
                    Properties.Resources.Filter_SaveProjectPlanFileType,
                    Properties.Resources.Filter_SaveProjectPlanFileExtension) == DialogResult.OK)
                {
                    string filename = m_FileDialogService.Filename;
                    if (string.IsNullOrWhiteSpace(filename))
                    {
                        DispatchNotification(
                            Properties.Resources.Title_Error,
                            Properties.Resources.Message_EmptyFilename);
                    }
                    else
                    {
                        ProjectPlanDto projectPlan = await BuildProjectPlanDtoAsync();
                        await SaveProjectPlanDtoAsync(projectPlan, filename);
                        IsProjectUpdated = false;
                        ProjectTitle = Path.GetFileNameWithoutExtension(filename);
                        m_AppSettingService.ProjectPlanFolder = Path.GetDirectoryName(filename);
                    }
                }
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

        public async Task DoImportMicrosoftProjectAsync()
        {
            try
            {
                IsBusy = true;
                if (IsProjectUpdated)
                {
                    var confirmation = new Confirmation()
                    {
                        Title = Properties.Resources.Title_UnsavedChanges,
                        Content = Properties.Resources.Message_UnsavedChanges
                    };
                    m_ConfirmationInteractionRequest.Raise(confirmation);
                    if (!confirmation.Confirmed)
                    {
                        return;
                    }
                }
                string directory = m_AppSettingService.ProjectPlanFolder;
                if (m_FileDialogService.ShowOpenDialog(
                    directory,
                    Properties.Resources.Filter_ImportMicrosoftProjectFileType,
                    Properties.Resources.Filter_ImportMicrosoftProjectFileExtension) == DialogResult.OK)
                {
                    string filename = m_FileDialogService.Filename;
                    if (string.IsNullOrWhiteSpace(filename))
                    {
                        DispatchNotification(
                            Properties.Resources.Title_Error,
                            Properties.Resources.Message_EmptyFilename);
                    }
                    else
                    {
                        MicrosoftProjectDto microsoftProjectDto = await ImportMicrosoftProjectAsync(filename);
                        ProcessMicrosoftProject(microsoftProjectDto);

                        HasStaleOutputs = true;
                        IsProjectUpdated = true;
                        ProjectTitle = Path.GetFileNameWithoutExtension(filename);

                        if (AutoCompile)
                        {
                            await RunCompileAsync();
                        }
                        m_AppSettingService.ProjectPlanFolder = Path.GetDirectoryName(filename);
                    }
                }
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

        public void DoCloseProject()
        {
            try
            {
                IsBusy = true;
                if (IsProjectUpdated)
                {
                    var confirmation = new Confirmation()
                    {
                        Title = Properties.Resources.Title_UnsavedChanges,
                        Content = Properties.Resources.Message_UnsavedChanges
                    };
                    m_ConfirmationInteractionRequest.Raise(confirmation);
                    if (!confirmation.Confirmed)
                    {
                        return;
                    }
                }
                ResetProject();
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

        public async Task DoOpenResourceSettingsAsync()
        {
            try
            {
                IsBusy = true;
                lock (m_Lock)
                {
                    var confirmation = new ResourceSettingsManagerConfirmation(DisableResources, ResourceDtos.Select(x => x.Copy()))
                    {
                        Title = Properties.Resources.Title_ResourceSettings
                    };
                    m_ResourceSettingsManagerInteractionRequest.Raise(confirmation);
                    if (!confirmation.Confirmed)
                    {
                        return;
                    }
                    ResourceDtos.Clear();
                    DisableResources = confirmation.DisableResources;
                    foreach (ResourceDto resourceDto in confirmation.ResourceDtos)
                    {
                        ResourceDtos.Add(resourceDto);
                    }
                    SetActivitiesTargetResources();
                }
                HasStaleOutputs = true;
                IsProjectUpdated = true;
                if (AutoCompile)
                {
                    await RunCompileAsync();
                }
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

        public void DoOpenArrowGraphSettings()
        {
            try
            {
                IsBusy = true;
                lock (m_Lock)
                {
                    var confirmation = new ArrowGraphSettingsManagerConfirmation(ArrowGraphSettingsDto.Copy())
                    {
                        Title = Properties.Resources.Title_ArrowGraphSettings
                    };
                    m_ArrowGraphSettingsManagerInteractionRequest.Raise(confirmation);
                    if (!confirmation.Confirmed)
                    {
                        return;
                    }
                    ArrowGraphSettingsDto = confirmation.ArrowGraphSettingsDto;
                }
                IsProjectUpdated = true;
                if (ArrowGraphDto != null)
                {
                    HasStaleArrowGraph = true;
                }
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

        public async Task DoCompileAsync()
        {
            try
            {
                IsBusy = true;
                await RunCompileAsync();
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

        public async Task DoAutoCompileAsync()
        {
            try
            {
                IsBusy = true;
                HasStaleOutputs = true;
                IsProjectUpdated = true;
                if (AutoCompile)
                {
                    await RunCompileAsync();
                }
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

        public async Task DoAddManagedActivityAsync()
        {
            try
            {
                IsBusy = true;
                lock (m_Lock)
                {
                    var activityId = m_VertexGraphCompiler.GetNextActivityId();
                    var dateTimeCalculator = new DateTimeCalculator();
                    dateTimeCalculator.UseBusinessDays(UseBusinessDays);
                    var activity = new ManagedActivityViewModel(
                        new DependentActivity<int>(activityId, 0),
                        ProjectStart,
                        ResourceDtos,
                        dateTimeCalculator,
                        m_EventService);
                    if (m_VertexGraphCompiler.AddActivity(activity))
                    {
                        Activities.Add(activity);
                    }
                }
                HasStaleOutputs = true;
                IsProjectUpdated = true;
                if (AutoCompile)
                {
                    await RunCompileAsync();
                }
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

        public async Task DoRemoveManagedActivityAsync()
        {
            try
            {
                IsBusy = true;
                lock (m_Lock)
                {
                    IEnumerable<ManagedActivityViewModel> activities = SelectedActivities.ToList();
                    if (!activities.Any())
                    {
                        return;
                    }
                    foreach (ManagedActivityViewModel activity in activities)
                    {
                        if (m_VertexGraphCompiler.RemoveActivity(activity.Id))
                        {
                            Activities.Remove(activity);
                        }
                    }
                }
                HasStaleOutputs = true;
                IsProjectUpdated = true;
                if (AutoCompile)
                {
                    await RunCompileAsync();
                }
            }
            catch (Exception ex)
            {
                DispatchNotification(
                    Properties.Resources.Title_Error,
                    ex.Message);
            }
            finally
            {
                SelectedActivities.Clear();
                RaisePropertyChanged(nameof(Activities));
                RaisePropertyChanged(nameof(SelectedActivities));
                IsBusy = false;
                RaiseCanExecuteChangedAllCommands();
            }
        }

        public async Task DoGenerateArrowGraphAsync()
        {
            try
            {
                IsBusy = true;
                await GenerateArrowGraphFromGraphCompilationAsync();
                IsProjectUpdated = true;
                m_EventService
                    .GetEvent<PubSubEvent<ArrowGraphGeneratedPayload>>()
                    .Publish(new ArrowGraphGeneratedPayload());
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

        #region IMainViewModel Members

        public string ProjectTitle
        {
            get
            {
                return m_ProjectTitle;
            }
            private set
            {
                m_ProjectTitle = value;
                RaisePropertyChanged(nameof(ProjectTitle));
            }
        }

        public bool IsProjectUpdated
        {
            get
            {
                return m_IsProjectUpdated;
            }
            private set
            {
                m_IsProjectUpdated = value;
                RaisePropertyChanged(nameof(IsProjectUpdated));
            }
        }

        public DateTime ProjectStart
        {
            get
            {
                return ProjectStartWithoutPublishing;
            }
            set
            {
                ProjectStartWithoutPublishing = value;
                PublishProjectStartUpdatedPayload();
            }
        }

        public bool ShowDates
        {
            get
            {
                return m_CoreViewModel.ShowDates;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.ShowDates = value;
                }
                RaisePropertyChanged();
                SetCompilationOutput();
            }
        }

        public bool UseBusinessDays
        {
            get
            {
                return UseBusinessDaysWithoutPublishing;
            }
            set
            {
                UseBusinessDaysWithoutPublishing = value;
                PublishUseBusinessDaysUpdatedPayload();
            }
        }

        public bool AutoCompile
        {
            get
            {
                return m_AutoCompile;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AutoCompile = value;
                }
                RaisePropertyChanged(nameof(AutoCompile));
            }
        }

        public ICommand OpenProjectPlanFileCommand
        {
            get;
            private set;
        }

        public ICommand SaveProjectPlanFileCommand
        {
            get;
            private set;
        }

        public ICommand ImportMicrosoftProjectCommand
        {
            get;
            private set;
        }

        public ICommand CloseProjectCommand
        {
            get;
            private set;
        }

        public ICommand OpenResourceSettingsCommand
        {
            get;
            private set;
        }

        public ICommand OpenArrowGraphSettingsCommand
        {
            get;
            private set;
        }

        public ICommand CompileCommand
        {
            get;
            private set;
        }

        public async Task DoOpenProjectPlanFileAsync()
        {
            await DoOpenProjectPlanFileAsync(string.Empty);
        }

        public async Task DoOpenProjectPlanFileAsync(string fileName)
        {
            try
            {
                IsBusy = true;
                if (IsProjectUpdated)
                {
                    var confirmation = new Confirmation
                    {
                        Title = Properties.Resources.Title_UnsavedChanges,
                        Content = Properties.Resources.Message_UnsavedChanges
                    };
                    m_ConfirmationInteractionRequest.Raise(confirmation);
                    if (!confirmation.Confirmed)
                    {
                        return;
                    }
                }
                string filename = fileName;
                if (string.IsNullOrWhiteSpace(filename))
                {
                    string directory = m_AppSettingService.ProjectPlanFolder;
                    if (m_FileDialogService.ShowOpenDialog(
                            directory,
                            Properties.Resources.Filter_OpenProjectPlanFileType,
                            Properties.Resources.Filter_OpenProjectPlanFileExtension) == DialogResult.OK)
                    {
                        filename = m_FileDialogService.Filename;
                    }
                    else
                    {
                        return;
                    }
                }
                if (string.IsNullOrWhiteSpace(filename))
                {
                    DispatchNotification(
                        Properties.Resources.Title_Error,
                        Properties.Resources.Message_EmptyFilename);
                }
                else
                {
                    ProjectPlanDto projectPlan = await OpenProjectPlanDtoAsync(filename);
                    ProcessProjectPlanDto(projectPlan);
                    IsProjectUpdated = false;
                    ProjectTitle = Path.GetFileNameWithoutExtension(filename);
                    m_AppSettingService.ProjectPlanFolder = Path.GetDirectoryName(filename);
                }
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

        public void ResetProject()
        {
            lock (m_Lock)
            {
                Activities.Clear();
                SelectedActivities.Clear();
                m_VertexGraphCompiler.Reset();

                ResourceDtos.Clear();
                DisableResources = false;

                ArrowGraphSettingsDto = m_SettingManager.GetArrowGraphSettings();
                GraphCompilation = new GraphCompilation<int, IDependentActivity<int>>(
                    false,
                    Enumerable.Empty<CircularDependency<int>>(),
                    Enumerable.Empty<int>(),
                    Enumerable.Empty<IDependentActivity<int>>(),
                    Enumerable.Empty<IResourceSchedule<int>>());

                CyclomaticComplexity = null;
                Duration = null;

                HasCompilationErrors = false;
                SetCompilationOutput();

                ArrowGraphDto = null;
                ArrowGraphData = GenerateArrowGraphData(ArrowGraphDto);
                HasStaleArrowGraph = false;

                ProjectStartWithoutPublishing = DateTime.UtcNow.BeginningOfDay();
                IsProjectUpdated = false;
                ProjectTitle = s_DefaultProjectTitle;

                HasStaleOutputs = false;
            }
            m_EventService
                .GetEvent<PubSubEvent<ArrowGraphGeneratedPayload>>()
                .Publish(new ArrowGraphGeneratedPayload());
        }

        #endregion

        #region IActivityManagerViewModel Members

        public bool ShowDays
        {
            get
            {
                return !ShowDates;
            }
        }

        public ICommand AddManagedActivityCommand
        {
            get;
            private set;
        }

        public ICommand RemoveManagedActivityCommand
        {
            get;
            private set;
        }

        #endregion

        #region IArrowGraphManagerViewModel Members

        public bool HasStaleArrowGraph
        {
            get
            {
                return m_HasStaleArrowGraph;
            }
            private set
            {
                m_HasStaleArrowGraph = value;
                RaisePropertyChanged(nameof(HasStaleArrowGraph));
            }
        }

        public ArrowGraphSettingsDto ArrowGraphSettingsDto
        {
            get
            {
                return m_CoreViewModel.ArrowGraphSettingsDto;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.ArrowGraphSettingsDto = value;
                }
            }
        }

        public ArrowGraphData ArrowGraphData
        {
            get;
            private set;
        }

        public ArrowGraphDto ArrowGraphDto
        {
            get;
            private set;
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
