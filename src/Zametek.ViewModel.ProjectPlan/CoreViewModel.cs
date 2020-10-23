using AutoMapper;
using Prism.Commands;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Event.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class CoreViewModel
        : PropertyChangedPubSubViewModel, ICoreViewModel
    {
        #region Fields

        private readonly object m_Lock;

        private const int c_MaxUndoRedoStackSize = 25;
        private readonly LimitedSizeStack<UndoRedoCommandPair> m_UndoStack;
        private readonly LimitedSizeStack<UndoRedoCommandPair> m_RedoStack;

        private readonly IProjectService m_ProjectService;
        private readonly ISettingService m_SettingService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly VertexGraphCompiler<int, int, IDependentActivity<int, int>> m_VertexGraphCompiler;
        private bool m_IsBusy;
        private DateTime m_ProjectStart;
        private bool m_IsProjectUpdated;
        private bool m_ShowDates;
        private bool m_UseBusinessDays;
        private bool m_HasStaleOutputs;
        private bool m_AutoCompile;
        private bool m_HasCompilationErrors;
        private IGraphCompilation<int, int, IDependentActivity<int, int>> m_GraphCompilation;
        private string m_CompilationOutput;
        private ArrowGraphModel m_ArrowGraphModel;
        private ArrowGraphSettingsModel m_ArrowGraphSettingsModel;
        private ResourceSettingsModel m_ResourceSettingsModel;
        private MetricsModel m_MetricsModel;
        private int? m_CyclomaticComplexity;
        private int? m_Duration;
        private double? m_DurationManMonths;
        private double? m_DirectCost;
        private double? m_IndirectCost;
        private double? m_OtherCost;
        private double? m_TotalCost;

        private readonly IMapper m_Mapper;
        private readonly IEventAggregator m_EventService;

        #endregion

        #region Ctors

        public CoreViewModel(
            IProjectService projectService,
            ISettingService settingService,
            IApplicationCommands applicationCommands,
            IDateTimeCalculator dateTimeCalculator,
            IMapper mapper,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_UndoStack = new LimitedSizeStack<UndoRedoCommandPair>(c_MaxUndoRedoStackSize);
            m_RedoStack = new LimitedSizeStack<UndoRedoCommandPair>(c_MaxUndoRedoStackSize);
            m_ProjectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            m_SettingService = settingService ?? throw new ArgumentNullException(nameof(settingService));
            ApplicationCommands = applicationCommands ?? throw new ArgumentNullException(nameof(applicationCommands));
            m_DateTimeCalculator = dateTimeCalculator ?? throw new ArgumentNullException(nameof(dateTimeCalculator));
            m_Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            m_VertexGraphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            Activities = new ObservableCollection<IManagedActivityViewModel>();
            InitializeCommands();
            ClearSettings();
        }

        #endregion

        #region Commands

        private void ReplaceCoreState(CoreStateModel coreState)
        {
            SetCoreState(coreState);
        }

        private bool CanReplaceCoreState(CoreStateModel coreState)
        {
            return true;
        }

        private void Undo()
        {
            lock (m_Lock)
            {
                UndoRedoCommandPair undoRedoCommandPair = m_UndoStack.Pop();
                undoRedoCommandPair.UndoCommand.Execute(undoRedoCommandPair.UndoParameter);
                m_RedoStack.Push(undoRedoCommandPair);
                RaiseCanExecuteChangedAllCommands();
            }
        }

        private bool CanUndo()
        {
            return m_UndoStack.Any();
        }

        private void Redo()
        {
            lock (m_Lock)
            {
                UndoRedoCommandPair undoRedoCommandPair = m_RedoStack.Pop();
                undoRedoCommandPair.RedoCommand.Execute(undoRedoCommandPair.RedoParameter);
                m_UndoStack.Push(undoRedoCommandPair);
                RaiseCanExecuteChangedAllCommands();
            }
        }

        private bool CanRedo()
        {
            return m_RedoStack.Any();
        }

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            ApplicationCommands.UndoCommand = new DelegateCommand(Undo, CanUndo);
            ApplicationCommands.RedoCommand = new DelegateCommand(Redo, CanRedo);
        }

        private void RaiseCanExecuteChangedAllCommands()
        {
            ApplicationCommands.UndoCommand.RaiseCanExecuteChanged();
            ApplicationCommands.RedoCommand.RaiseCanExecuteChanged();
        }

        private CoreStateModel GetCoreState()
        {
            lock (m_Lock)
            {
                IEnumerable<IDependentActivity<int, int>> activities = Activities.Select(x => (IDependentActivity<int, int>)x.CloneObject());

                return new CoreStateModel
                {
                    ArrowGraphSettings = ArrowGraphSettings.CloneObject(),
                    ResourceSettings = ResourceSettings.CloneObject(),
                    DependentActivities = m_Mapper.Map<IEnumerable<IDependentActivity<int, int>>, IEnumerable<DependentActivityModel>>(activities),
                    ProjectStart = ProjectStart,
                    UseBusinessDays = UseBusinessDays,
                    ShowDates = ShowDates,
                };
            }
        }

        private void SetCoreState(CoreStateModel coreState)
        {
            if (coreState is null)
            {
                return;
            }

            lock (m_Lock)
            {
                try
                {
                    IsBusy = true;

                    ClearManagedActivities();

                    m_ArrowGraphSettingsModel = coreState.ArrowGraphSettings;
                    m_ResourceSettingsModel = coreState.ResourceSettings;

                    m_ProjectStart = coreState.ProjectStart;
                    m_UseBusinessDays = coreState.UseBusinessDays;
                    m_ShowDates = coreState.ShowDates;
                    RaisePropertyChanged(nameof(ProjectStart));
                    RaisePropertyChanged(nameof(UseBusinessDays));
                    RaisePropertyChanged(nameof(ShowDates));

                    AddManagedActivities(new HashSet<DependentActivityModel>(coreState.DependentActivities));

                    RunAutoCompile();

                    RecordCoreState();
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private void PublishGraphCompiledPayload()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompiledPayload>>()
                .Publish(new GraphCompiledPayload());
        }

        private void PublishGraphCompilationUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompilationUpdatedPayload>>()
                .Publish(new GraphCompilationUpdatedPayload());
        }

        private string BuildCircularDependenciesErrorMessage(IEnumerable<ICircularDependency<int>> circularDependencies)
        {
            if (circularDependencies == null)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@">{Resource.ProjectPlan.Resources.Message_CircularDependencies}");
            foreach (CircularDependency<int> circularDependency in circularDependencies)
            {
                output.AppendLine(string.Join(@" -> ", circularDependency.Dependencies));
            }
            return output.ToString();
        }

        private string BuildMissingDependenciesErrorMessage(IEnumerable<int> missingDependencies)
        {
            if (missingDependencies == null)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@">{Resource.ProjectPlan.Resources.Message_MissingDependencies}");
            foreach (int missingDependency in missingDependencies)
            {
                IList<int> activities = Activities
                    .Where(x => x.Dependencies.Contains(missingDependency))
                    .Select(x => x.Id)
                    .ToList();
                output.AppendFormat(CultureInfo.InvariantCulture, $@"{missingDependency} -> ");
                output.AppendLine(string.Join(@", ", activities));
            }
            return output.ToString();
        }

        private string BuildInvalidConstraintsErrorMessage(IEnumerable<int> invalidConstraints)
        {
            if (invalidConstraints == null)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@">{Resource.ProjectPlan.Resources.Message_InvalidConstraints} {string.Join(@", ", invalidConstraints)}");
            return output.ToString();
        }

        private string BuildActivitySchedules(IEnumerable<ResourceSeriesModel> resourceSeriesSet)
        {
            lock (m_Lock)
            {
                if (resourceSeriesSet == null)
                {
                    return string.Empty;
                }

                var output = new StringBuilder();

                foreach (ResourceSeriesModel resourceSeries in resourceSeriesSet)
                {
                    IEnumerable<ScheduledActivityModel> scheduledActivities = resourceSeries?.ResourceSchedule?.ScheduledActivities;
                    if (scheduledActivities == null)
                    {
                        continue;
                    }
                    output.AppendLine($@">{resourceSeries.Title}");
                    int previousFinishTime = 0;
                    foreach (ScheduledActivityModel scheduledActivity in scheduledActivities)
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
                        output.AppendLine($@"{Resource.ProjectPlan.Resources.Label_Activity} {scheduledActivity.Id}: {start} -> {finish}");
                        previousFinishTime = finishTime;
                    }
                    output.AppendLine();
                }
                return output.ToString();
            }
        }

        #endregion

        #region ICoreViewModel Members

        public bool IsBusy
        {
            get
            {
                return m_IsBusy;
            }
            set
            {
                lock (m_Lock)
                {
                    m_IsBusy = value;
                }
                RaisePropertyChanged();
            }
        }

        public DateTime ProjectStart
        {
            get
            {
                return m_ProjectStart;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ProjectStart = value;
                    RecordCoreState();
                }
                RaisePropertyChanged();
            }
        }

        public bool IsProjectUpdated
        {
            get
            {
                return m_IsProjectUpdated;
            }
            set
            {
                lock (m_Lock)
                {
                    m_IsProjectUpdated = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool ShowDates
        {
            get
            {
                return m_ShowDates;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ShowDates = value;
                    RecordCoreState();
                }
                RaisePropertyChanged();
            }
        }

        public bool UseBusinessDays
        {
            get
            {
                return m_UseBusinessDays;
            }
            set
            {
                lock (m_Lock)
                {
                    m_UseBusinessDays = value;
                    m_DateTimeCalculator.UseBusinessDays(value);
                    RecordCoreState();
                }
                RaisePropertyChanged();
            }
        }

        public bool HasStaleOutputs
        {
            get
            {
                return m_HasStaleOutputs;
            }
            set
            {
                lock (m_Lock)
                {
                    m_HasStaleOutputs = value;
                }
                RaisePropertyChanged();
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
                RaisePropertyChanged();
            }
        }

        public bool HasCompilationErrors
        {
            get
            {
                return m_HasCompilationErrors;
            }
            set
            {
                lock (m_Lock)
                {
                    m_HasCompilationErrors = value;
                }
                RaisePropertyChanged();
            }
        }

        public IGraphCompilation<int, int, IDependentActivity<int, int>> GraphCompilation
        {
            get
            {
                return m_GraphCompilation;
            }
            set
            {
                lock (m_Lock)
                {
                    m_GraphCompilation = value;
                }
                RaisePropertyChanged();
            }
        }

        public string CompilationOutput
        {
            get
            {
                return m_CompilationOutput;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CompilationOutput = value;
                }
                RaisePropertyChanged();
            }
        }

        public ArrowGraphModel ArrowGraph
        {
            get
            {
                return m_ArrowGraphModel;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ArrowGraphModel = value;
                }
                RaisePropertyChanged();
            }
        }

        public MetricsModel Metrics
        {
            get
            {
                return m_MetricsModel;
            }
            set
            {
                lock (m_Lock)
                {
                    m_MetricsModel = value;
                }
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<IManagedActivityViewModel> Activities
        {
            get;
        }

        public ArrowGraphSettingsModel ArrowGraphSettings
        {
            get
            {
                return m_ArrowGraphSettingsModel;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_ArrowGraphSettingsModel = value;
                }
                RaisePropertyChanged();
            }
        }

        public ResourceSettingsModel ResourceSettings
        {
            get
            {
                return m_ResourceSettingsModel;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_ResourceSettingsModel = value;
                }
                RaisePropertyChanged();
            }
        }

        public ResourceSeriesSetModel ResourceSeriesSet
        {
            get;
            private set;
        }

        public IApplicationCommands ApplicationCommands
        {
            get;
        }

        public int? CyclomaticComplexity
        {
            get
            {
                return m_CyclomaticComplexity;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CyclomaticComplexity = value;
                }
                RaisePropertyChanged();
            }
        }

        public int? Duration
        {
            get
            {
                return m_Duration;
            }
            set
            {
                lock (m_Lock)
                {
                    m_Duration = value;
                }
                RaisePropertyChanged();
            }
        }

        public double? DurationManMonths
        {
            get
            {
                return m_DurationManMonths;
            }
            set
            {
                lock (m_Lock)
                {
                    m_DurationManMonths = value;
                }
                RaisePropertyChanged();
            }
        }

        public double? DirectCost
        {
            get
            {
                return m_DirectCost;
            }
            set
            {
                lock (m_Lock)
                {
                    m_DirectCost = value;
                }
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(Efficiency));
            }
        }

        public double? IndirectCost
        {
            get
            {
                return m_IndirectCost;
            }
            set
            {
                lock (m_Lock)
                {
                    m_IndirectCost = value;
                }
                RaisePropertyChanged();
            }
        }

        public double? OtherCost
        {
            get
            {
                return m_OtherCost;
            }
            set
            {
                lock (m_Lock)
                {
                    m_OtherCost = value;
                }
                RaisePropertyChanged();
            }
        }

        public double? TotalCost
        {
            get
            {
                return m_TotalCost;
            }
            set
            {
                lock (m_Lock)
                {
                    m_TotalCost = value;
                }
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(Efficiency));
            }
        }

        public double? Efficiency
        {
            get
            {
                if (!TotalCost.HasValue || TotalCost.Value == 0)
                {
                    return null;
                }
                return DirectCost / TotalCost;
            }
        }

        public CoreStateModel CoreState
        {
            get;
            private set;
        }

        public void RecordCoreState()
        {
            lock (m_Lock)
            {
                CoreState = GetCoreState();
            }
        }

        public void RecordRedoUndo(Action action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            lock (m_Lock)
            {
                CoreStateModel before = CoreState;
                action();
                CoreStateModel after = GetCoreState();

                ClearRedoStack();
                m_UndoStack.Push(new UndoRedoCommandPair(
                    new DelegateCommand<CoreStateModel>(ReplaceCoreState, CanReplaceCoreState), before,
                    new DelegateCommand<CoreStateModel>(ReplaceCoreState, CanReplaceCoreState), after));

                RaiseCanExecuteChangedAllCommands();
            }
        }

        public void ClearUndoStack()
        {
            lock (m_Lock)
            {
                m_UndoStack.Clear();
                RecordCoreState();
            }
        }

        public void ClearRedoStack()
        {
            lock (m_Lock)
            {
                m_RedoStack.Clear();
            }
        }

        public void AddManagedActivity()
        {
            lock (m_Lock)
            {
                var activityId = m_VertexGraphCompiler.GetNextActivityId();

                var set = new HashSet<DependentActivityModel>();
                set.Add(new DependentActivityModel
                {
                    Activity = new ActivityModel
                    {
                        Id = activityId,
                        Duration = 0,
                        TargetResources = new List<int>(),
                        AllocatedToResources = new List<int>(),
                    },
                    Dependencies = new List<int>(),
                    ResourceDependencies = new List<int>(),
                });
                AddManagedActivities(set);
            }
        }

        public void AddManagedActivities(HashSet<DependentActivityModel> dependentActivities)
        {
            if (dependentActivities == null)
            {
                throw new ArgumentNullException(nameof(dependentActivities));
            }

            lock (m_Lock)
            {
                foreach (DependentActivityModel dependentActivity in dependentActivities)
                {
                    var dateTimeCalculator = new DateTimeCalculator();
                    dateTimeCalculator.UseBusinessDays(UseBusinessDays);

                    var activity = new ManagedActivityViewModel(
                        m_Mapper.Map<DependentActivityModel, DependentActivity<int, int>>(dependentActivity),
                        ProjectStart,
                        dependentActivity.Activity?.MinimumEarliestStartDateTime,
                        dependentActivity.Activity?.MaximumLatestFinishDateTime,
                        ResourceSettings.Resources,
                        dateTimeCalculator,
                        m_EventService);

                    if (m_VertexGraphCompiler.AddActivity(activity))
                    {
                        Activities.Add(activity);
                    }
                }

                RecordCoreState();
            }
        }

        public void RemoveManagedActivities(HashSet<int> dependentActivityIds)
        {
            if (dependentActivityIds == null)
            {
                throw new ArgumentNullException(nameof(dependentActivityIds));
            }

            lock (m_Lock)
            {
                IEnumerable<IManagedActivityViewModel> dependentActivities = Activities.Where(x => dependentActivityIds.Contains(x.Id)).ToList();

                foreach (IManagedActivityViewModel dependentActivity in dependentActivities)
                {
                    if (m_VertexGraphCompiler.RemoveActivity(dependentActivity.Id))
                    {
                        Activities.Remove(dependentActivity);
                    }
                }

                RecordCoreState();
            }
        }

        public void ClearManagedActivities()
        {
            lock (m_Lock)
            {
                Activities.Clear();
                m_VertexGraphCompiler.Reset();
            }
        }

        public void UpdateArrowGraphSettings(ArrowGraphSettingsModel arrowGraphSettings)
        {
            if (arrowGraphSettings is null)
            {
                throw new ArgumentNullException(nameof(arrowGraphSettings));
            }

            lock (m_Lock)
            {
                ArrowGraphSettings = arrowGraphSettings;
                RecordCoreState();
            }
        }

        public void UpdateResourceSettings(ResourceSettingsModel resourceSettings)
        {
            if (resourceSettings == null)
            {
                throw new ArgumentNullException(nameof(resourceSettings));
            }

            lock (m_Lock)
            {

                ResourceSettings = resourceSettings;

                foreach (IManagedActivityViewModel activity in Activities)
                {
                    activity.SetTargetResources(ResourceSettings.Resources.Select(x => x.CloneObject()));
                }

                RecordCoreState();
            }
        }

        public void UpdateActivitiesTargetResourceDependencies()
        {
            lock (m_Lock)
            {
                foreach (IManagedActivityViewModel activity in Activities.Where(x => x.HasUpdatedDependencies))
                {
                    m_VertexGraphCompiler.SetActivityDependencies(activity.Id, new HashSet<int>(activity.UpdatedDependencies));
                    activity.UpdatedDependencies.Clear();
                    activity.HasUpdatedDependencies = false;
                }

                RecordCoreState();
            }
        }

        public void UpdateActivitiesAllocatedToResources()
        {
            lock (m_Lock)
            {
                foreach (IManagedActivityViewModel activity in Activities)
                {
                    activity.UpdateAllocatedToResources();
                }
            }
        }

        public void UpdateActivitiesProjectStart()
        {
            lock (m_Lock)
            {
                foreach (IManagedActivityViewModel activity in Activities)
                {
                    activity.ProjectStart = ProjectStart;
                }
            }
        }

        public void UpdateActivitiesUseBusinessDays()
        {
            lock (m_Lock)
            {
                foreach (IManagedActivityViewModel activity in Activities)
                {
                    activity.UseBusinessDays(UseBusinessDays);
                }
            }
        }

        public int RunCalculateResourcedCyclomaticComplexity()
        {
            lock (m_Lock)
            {
                int resourcedCyclomaticComplexity = 0;

                // Cyclomatic complexity is calculated against the transitively reduced
                // vertex graph, where resource dependencies are taken into account along
                // with regular dependencies.
                if (GraphCompilation != null
                    && !HasCompilationErrors
                    && !HasStaleOutputs
                    && !ResourceSettings.AreDisabled)
                {
                    IList<IDependentActivity<int, int>> dependentActivities =
                        GraphCompilation.DependentActivities
                        .Select(x => (IDependentActivity<int, int>)x.CloneObject())
                        .ToList();

                    if (dependentActivities.Any())
                    {
                        var vertexGraphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
                        foreach (DependentActivity<int, int> dependentActivity in dependentActivities)
                        {
                            dependentActivity.Dependencies.UnionWith(dependentActivity.ResourceDependencies);
                            dependentActivity.ResourceDependencies.Clear();
                            vertexGraphCompiler.AddActivity(dependentActivity);
                        }

                        vertexGraphCompiler.TransitiveReduction();
                        resourcedCyclomaticComplexity = vertexGraphCompiler.CyclomaticComplexity;
                    }
                }

                return resourcedCyclomaticComplexity;
            }
        }

        public void RunCompile()
        {
            lock (m_Lock)
            {
                UpdateActivitiesTargetResourceDependencies();

                var availableResources = new List<IResource<int>>();
                if (!ResourceSettings.AreDisabled)
                {
                    availableResources.AddRange(m_Mapper.Map<IEnumerable<ResourceModel>, IEnumerable<Resource<int>>>(ResourceSettings.Resources));
                }

                GraphCompilation = m_VertexGraphCompiler.Compile(availableResources);

                Duration = m_VertexGraphCompiler.Duration;

                DurationManMonths = CalculateDurationManMonths();

                UpdateActivitiesAllocatedToResources();

                CalculateResourceSeriesSet();

                SetCompilationOutput();

                CalculateCosts();

                IsProjectUpdated = true;

                // Cyclomatic complexity is calculated against the vertex graph without resource dependencies.
                CyclomaticComplexity = null;

                if (!HasCompilationErrors)
                {
                    IList<IDependentActivity<int, int>> dependentActivities =
                        GraphCompilation.DependentActivities
                        .Select(x => (IDependentActivity<int, int>)x.CloneObject())
                        .ToList();

                    if (dependentActivities.Any())
                    {
                        var vertexGraphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
                        foreach (DependentActivity<int, int> dependentActivity in dependentActivities)
                        {
                            dependentActivity.ResourceDependencies.Clear();
                            vertexGraphCompiler.AddActivity(dependentActivity);
                        }

                        vertexGraphCompiler.TransitiveReduction();
                        CyclomaticComplexity = vertexGraphCompiler.CyclomaticComplexity;
                    }
                }

                HasStaleOutputs = false;
            }

            PublishGraphCompiledPayload();
            PublishGraphCompilationUpdatedPayload();
        }

        public void RunAutoCompile()
        {
            if (AutoCompile)
            {
                RunCompile();
            }
        }

        public void RunTransitiveReduction()
        {
            lock (m_Lock)
            {
                UpdateActivitiesTargetResourceDependencies();
                m_VertexGraphCompiler.Compile(new List<IResource<int>>());
                m_VertexGraphCompiler.TransitiveReduction();
                RunCompile();
            }
        }

        public void SetCompilationOutput()
        {
            lock (m_Lock)
            {
                IGraphCompilation<int, int, IDependentActivity<int, int>> graphCompilation = GraphCompilation;
                CompilationOutput = string.Empty;
                HasCompilationErrors = false;
                if (graphCompilation == null)
                {
                    return;
                }
                var output = new StringBuilder();

                if (graphCompilation.Errors != null)
                {
                    if (graphCompilation.Errors.AllResourcesExplicitTargetsButNotAllActivitiesTargeted)
                    {
                        HasCompilationErrors = true;
                        output.AppendLine($@">{Resource.ProjectPlan.Resources.Message_AllResourcesExplicitTargetsNotAllActivitiesTargeted}");
                    }

                    if (graphCompilation.Errors.CircularDependencies.Any())
                    {
                        HasCompilationErrors = true;
                        output.Append(BuildCircularDependenciesErrorMessage(graphCompilation.Errors.CircularDependencies));
                    }

                    if (graphCompilation.Errors.MissingDependencies.Any())
                    {
                        HasCompilationErrors = true;
                        output.Append(BuildMissingDependenciesErrorMessage(graphCompilation.Errors.MissingDependencies));
                    }

                    if (graphCompilation.Errors.InvalidConstraints.Any())
                    {
                        HasCompilationErrors = true;
                        output.Append(BuildInvalidConstraintsErrorMessage(graphCompilation.Errors.InvalidConstraints));
                    }
                }

                if (ResourceSeriesSet?.Scheduled != null
                    && ResourceSeriesSet.Scheduled.Any()
                    && !HasCompilationErrors)
                {
                    output.Append(BuildActivitySchedules(ResourceSeriesSet.Scheduled));
                }

                if (HasCompilationErrors)
                {
                    output.Insert(0, Environment.NewLine);
                    output.Insert(0, $@">{Resource.ProjectPlan.Resources.Message_CompilationErrors}");
                }

                CompilationOutput = output.ToString();
            }
        }

        public void CalculateResourceSeriesSet()
        {
            lock (m_Lock)
            {
                ClearResourceSeriesSet();

                IEnumerable<IResourceSchedule<int, int>> resourceSchedules = GraphCompilation?.ResourceSchedules;
                IList<ResourceModel> resources = ResourceSettings.Resources;

                if (resourceSchedules == null || resources == null)
                {
                    return;
                }

                ResourceSeriesSet = m_ProjectService.CalculateResourceSeriesSet(
                    m_Mapper.Map<IEnumerable<IResourceSchedule<int, int>>, IList<ResourceScheduleModel>>(resourceSchedules),
                    resources,
                    ResourceSettings.DefaultUnitCost);
            }
        }

        public void ClearResourceSeriesSet()
        {
            lock (m_Lock)
            {
                ResourceSeriesSet = null;
            }
        }

        public void CalculateCosts()
        {
            lock (m_Lock)
            {
                ClearCosts();

                if (HasCompilationErrors)
                {
                    return;
                }

                CostsModel costs = ResourceSeriesSet != null ? m_ProjectService.CalculateProjectCosts(ResourceSeriesSet) : new CostsModel();
                DirectCost = costs.DirectCost;
                IndirectCost = costs.IndirectCost;
                OtherCost = costs.OtherCost;
                TotalCost = costs.TotalCost;
            }
        }

        public double CalculateDurationManMonths()
        {
            lock (m_Lock)
            {
                int? durationManDays = Duration;
                if (!durationManDays.HasValue)
                {
                    return 0;
                }
                m_DateTimeCalculator.UseBusinessDays(UseBusinessDays);
                int daysPerWeek = m_DateTimeCalculator.DaysPerWeek;
                return durationManDays.GetValueOrDefault() / (daysPerWeek * 52 / 12.0);
            }
        }

        public void ClearCosts()
        {
            lock (m_Lock)
            {
                DirectCost = null;
                IndirectCost = null;
                OtherCost = null;
                TotalCost = null;
            }
        }

        public void ClearSettings()
        {
            ArrowGraphSettings = m_SettingService.DefaultArrowGraphSettings;
            ResourceSettings = m_SettingService.DefaultResourceSettings;
        }

        #endregion
    }
}
