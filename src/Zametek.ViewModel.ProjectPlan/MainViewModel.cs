using AutoMapper;
using FluentDateTime;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Data.ProjectPlan;
using Zametek.Event.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class MainViewModel
        : PropertyChangedPubSubViewModel, IMainViewModel
    {
        #region Fields

        private readonly object m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IFileDialogService m_FileDialogService;
        private readonly ISettingService m_SettingService;
        private readonly IMapper m_Mapper;
        private readonly IEventAggregator m_EventService;
        private readonly InteractionRequest<Notification> m_NotificationInteractionRequest;
        private readonly InteractionRequest<Confirmation> m_ConfirmationInteractionRequest;
        private readonly InteractionRequest<ResourceSettingsManagerConfirmation> m_ResourceSettingsManagerInteractionRequest;
        private readonly InteractionRequest<ArrowGraphSettingsManagerConfirmation> m_ArrowGraphSettingsManagerInteractionRequest;
        private readonly InteractionRequest<Notification> m_AboutInteractionRequest;

        private SubscriptionToken m_ApplicationClosingSubscriptionToken;

        #endregion

        #region Ctors

        public MainViewModel(
            ICoreViewModel coreViewModel,
            IFileDialogService fileDialogService,
            ISettingService settingService,
            IMapper mapper,
            IApplicationCommands applicationCommands,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_FileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            m_SettingService = settingService ?? throw new ArgumentNullException(nameof(settingService));
            m_Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            ApplicationCommands = applicationCommands ?? throw new ArgumentNullException(nameof(applicationCommands));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            m_NotificationInteractionRequest = new InteractionRequest<Notification>();
            m_ConfirmationInteractionRequest = new InteractionRequest<Confirmation>();
            m_ResourceSettingsManagerInteractionRequest = new InteractionRequest<ResourceSettingsManagerConfirmation>();
            m_ArrowGraphSettingsManagerInteractionRequest = new InteractionRequest<ArrowGraphSettingsManagerConfirmation>();
            m_AboutInteractionRequest = new InteractionRequest<Notification>();

            ResetProject();

            ShowDates = false;
            UseBusinessDaysWithoutPublishing = true;
            AutoCompile = true;
            m_CoreViewModel.ClearUndoStack();

            InitializeCommands();
            SubscribeToEvents();

            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.IsBusy), nameof(IsBusy), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.ProjectStart), nameof(ProjectStart), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.IsProjectUpdated), nameof(IsProjectUpdated), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.IsProjectUpdated), nameof(Title), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.ShowDates), nameof(ShowDates), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.UseBusinessDays), nameof(UseBusinessDays), ThreadOption.BackgroundThread);

            PropertyChanged += (sender, args) =>
            {
                if (string.CompareOrdinal(args.PropertyName, nameof(IsProjectUpdated)) == 0)
                {
                    RaiseCanExecuteChangedAllCommands();
                }
            };
        }

        #endregion

        #region Properties

        private bool HasStaleOutputs
        {
            get
            {
                return m_CoreViewModel.HasStaleOutputs;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.HasStaleOutputs = value;
                }
            }
        }

        private DateTime ProjectStartWithoutPublishing
        {
            get
            {
                return m_CoreViewModel.ProjectStart;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.RecordRedoUndo(() =>
                    {
                        m_CoreViewModel.ProjectStart = value;
                    });
                    IsProjectUpdated = true;
                }
                RaisePropertyChanged(nameof(ProjectStart));
            }
        }

        private bool UseBusinessDaysWithoutPublishing
        {
            get
            {
                return m_CoreViewModel.UseBusinessDays;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.RecordRedoUndo(() =>
                    {
                        m_CoreViewModel.UseBusinessDays = value;
                    });
                }
                RaisePropertyChanged(nameof(UseBusinessDays));
            }
        }

        private IList<IManagedActivityViewModel> Activities => m_CoreViewModel.Activities;

        private bool HasCompilationErrors
        {
            get
            {
                return m_CoreViewModel.HasCompilationErrors;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.HasCompilationErrors = value;
                }
            }
        }

        private IGraphCompilation<int, int, IDependentActivity<int, int>> GraphCompilation
        {
            get
            {
                return m_CoreViewModel.GraphCompilation;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.GraphCompilation = value;
                }
            }
        }

        private int? CyclomaticComplexity
        {
            get
            {
                return m_CoreViewModel.CyclomaticComplexity;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.CyclomaticComplexity = value;
                }
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
                lock (m_Lock)
                {
                    m_CoreViewModel.Duration = value;
                }
            }
        }

        private double? DurationManMonths
        {
            get
            {
                return m_CoreViewModel.DurationManMonths;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.DurationManMonths = value;
                }
            }
        }

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

        #endregion

        #region Commands

        private DelegateCommandBase InternalNewProjectPlanFileCommand
        {
            get;
            set;
        }

        private async void NewProjectPlanFile()
        {
            await DoNewProjectPlanFileAsync().ConfigureAwait(true);
        }

        private DelegateCommandBase InternalOpenProjectPlanFileCommand
        {
            get;
            set;
        }

        private async void OpenProjectPlanFile()
        {
            await DoOpenProjectPlanFileAsync().ConfigureAwait(true);
        }

        private DelegateCommandBase InternalSaveProjectPlanFileCommand
        {
            get;
            set;
        }

        private async void SaveProjectPlanFile()
        {
            await DoSaveProjectPlanFileAsync().ConfigureAwait(true);
        }

        private bool CanSaveProjectPlanFile()
        {
            return IsProjectUpdated;
        }

        private DelegateCommandBase InternalSaveAsProjectPlanFileCommand
        {
            get;
            set;
        }

        private async void SaveAsProjectPlanFile()
        {
            await DoSaveAsProjectPlanFileAsync().ConfigureAwait(true);
        }

        private DelegateCommandBase InternalImportProjectCommand
        {
            get;
            set;
        }

        private async void ImportProject()
        {
            await DoImportProjectAsync().ConfigureAwait(true);
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

        private DelegateCommandBase InternalOpenResourceSettingsCommand
        {
            get;
            set;
        }

        private async void OpenResourceSettings()
        {
            await DoOpenResourceSettingsAsync().ConfigureAwait(true);
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

        private DelegateCommandBase InternalToggleShowDatesCommand
        {
            get;
            set;
        }

        private void ToggleShowDates()
        {
            ShowDates = !ShowDates;
        }

        private DelegateCommandBase InternalToggleUseBusinessDaysCommand
        {
            get;
            set;
        }

        private void ToggleUseBusinessDays()
        {
            UseBusinessDays = !UseBusinessDays;
        }

        private DelegateCommandBase InternalCalculateResourcedCyclomaticComplexityCommand
        {
            get;
            set;
        }

        private async void CalculateResourcedCyclomaticComplexity()
        {
            await DoCalculateResourcedCyclomaticComplexityAsync().ConfigureAwait(true);
        }

        private bool CanCalculateResourcedCyclomaticComplexity()
        {
            return GraphCompilation != null
                && !HasCompilationErrors
                && !HasStaleOutputs
                && !ResourceSettings.AreDisabled;
        }

        private DelegateCommandBase InternalCompileCommand
        {
            get;
            set;
        }

        private async void Compile()
        {
            await DoCompileAsync().ConfigureAwait(true);
        }

        private bool CanCompile()
        {
            return !IsBusy;
        }

        private DelegateCommandBase InternalTransitiveReductionCommand
        {
            get;
            set;
        }

        private async void TransitiveReduction()
        {
            await DoTransitiveReductionAsync().ConfigureAwait(true);
        }

        private bool CanTransitiveReduction()
        {
            return !IsBusy;
        }

        private DelegateCommandBase InternalOpenHyperLinkCommand
        {
            get;
            set;
        }

        private void OpenHyperLink(string hyperlink)
        {
            DoOpenHyperLink(hyperlink);
        }

        private DelegateCommandBase InternalOpenAboutCommand
        {
            get;
            set;
        }

        private DelegateCommandBase InternalExportScenariosCommand
        {
            get;
            set;
        }

        private DelegateCommandBase InternalExportCsvCommand
        {
            get;
            set;
        }

        private void OpenAbout()
        {
            DoOpenAbout();
        }

        private async void ExportScenarios()
        {
            await DoExportScenariosAsync().ConfigureAwait(true);
        }

        private async void ExportCsv()
        {
            await DoExportCsvAsync().ConfigureAwait(true);
        }

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            NewProjectPlanFileCommand =
                InternalNewProjectPlanFileCommand =
                    new DelegateCommand(NewProjectPlanFile);
            OpenProjectPlanFileCommand =
                InternalOpenProjectPlanFileCommand =
                    new DelegateCommand(OpenProjectPlanFile);
            SaveProjectPlanFileCommand =
                InternalSaveProjectPlanFileCommand =
                    new DelegateCommand(SaveProjectPlanFile, CanSaveProjectPlanFile);
            SaveAsProjectPlanFileCommand =
                InternalSaveAsProjectPlanFileCommand =
                    new DelegateCommand(SaveAsProjectPlanFile);
            ImportProjectCommand =
                InternalImportProjectCommand =
                    new DelegateCommand(ImportProject);
            CloseProjectCommand =
                InternalCloseProjectCommand =
                    new DelegateCommand(CloseProject);
            OpenResourceSettingsCommand =
                InternalOpenResourceSettingsCommand =
                    new DelegateCommand(OpenResourceSettings);
            OpenArrowGraphSettingsCommand =
                InternalOpenArrowGraphSettingsCommand =
                    new DelegateCommand(OpenArrowGraphSettings);
            ToggleShowDatesCommand =
                InternalToggleShowDatesCommand =
                    new DelegateCommand(ToggleShowDates);
            ToggleUseBusinessDaysCommand =
                InternalToggleUseBusinessDaysCommand =
                    new DelegateCommand(ToggleUseBusinessDays);
            CalculateResourcedCyclomaticComplexityCommand =
                InternalCalculateResourcedCyclomaticComplexityCommand =
                    new DelegateCommand(CalculateResourcedCyclomaticComplexity, CanCalculateResourcedCyclomaticComplexity);
            CompileCommand =
                InternalCompileCommand =
                    new DelegateCommand(Compile, CanCompile);
            TransitiveReductionCommand =
                InternalTransitiveReductionCommand =
                    new DelegateCommand(TransitiveReduction, CanTransitiveReduction);
            OpenHyperLinkCommand =
                InternalOpenHyperLinkCommand =
                    new DelegateCommand<string>(OpenHyperLink);
            OpenAboutCommand =
                InternalOpenAboutCommand =
                    new DelegateCommand(OpenAbout);
            ExportScenariosCommand =
                InternalExportScenariosCommand =
                    new DelegateCommand(ExportScenarios);
            ExportCsvCommand =
                InternalExportCsvCommand =
                    new DelegateCommand(ExportCsv);
        }

        private void RaiseCanExecuteChangedAllCommands()
        {
            InternalNewProjectPlanFileCommand.RaiseCanExecuteChanged();
            InternalOpenProjectPlanFileCommand.RaiseCanExecuteChanged();
            InternalSaveProjectPlanFileCommand.RaiseCanExecuteChanged();
            InternalSaveAsProjectPlanFileCommand.RaiseCanExecuteChanged();
            InternalImportProjectCommand.RaiseCanExecuteChanged();
            InternalCloseProjectCommand.RaiseCanExecuteChanged();
            InternalOpenResourceSettingsCommand.RaiseCanExecuteChanged();
            InternalOpenArrowGraphSettingsCommand.RaiseCanExecuteChanged();
            InternalToggleShowDatesCommand.RaiseCanExecuteChanged();
            InternalToggleUseBusinessDaysCommand.RaiseCanExecuteChanged();
            InternalCalculateResourcedCyclomaticComplexityCommand.RaiseCanExecuteChanged();
            InternalCompileCommand.RaiseCanExecuteChanged();
            InternalTransitiveReductionCommand.RaiseCanExecuteChanged();
            InternalOpenHyperLinkCommand.RaiseCanExecuteChanged();
            InternalOpenAboutCommand.RaiseCanExecuteChanged();
            InternalExportScenariosCommand.RaiseCanExecuteChanged();
            InternalExportCsvCommand.RaiseCanExecuteChanged();

            ApplicationCommands.UndoCommand.RaiseCanExecuteChanged();
            ApplicationCommands.RedoCommand.RaiseCanExecuteChanged();
        }

        private void SubscribeToEvents()
        {
            m_ApplicationClosingSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<ApplicationClosingPayload>>()
                    .Subscribe(payload =>
                    {
                        try
                        {
                            IsBusy = true;
                            if (IsProjectUpdated)
                            {
                                var confirmation = new Confirmation
                                {
                                    Title = Resource.ProjectPlan.Resources.Title_UnsavedChanges,
                                    Content = Resource.ProjectPlan.Resources.Message_UnsavedChanges
                                };
                                m_ConfirmationInteractionRequest.Raise(confirmation);
                                if (!confirmation.Confirmed)
                                {
                                    payload.IsCanceled = true;
                                }
                            }
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
                    }, ThreadOption.PublisherThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<ApplicationClosingPayload>>()
                .Unsubscribe(m_ApplicationClosingSubscriptionToken);
        }

        private void PublishProjectStartUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<ProjectStartUpdatedPayload>>()
                .Publish(new ProjectStartUpdatedPayload());
        }

        private void PublishUseBusinessDaysUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<UseBusinessDaysUpdatedPayload>>()
                .Publish(new UseBusinessDaysUpdatedPayload());
        }

        private void PublishShowDatesUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<ShowDatesUpdatedPayload>>()
                .Publish(new ShowDatesUpdatedPayload());
        }

        private void PublishArrowGraphUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<ArrowGraphUpdatedPayload>>()
                .Publish(new ArrowGraphUpdatedPayload());
        }

        private void PublishGanttChartUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<GanttChartUpdatedPayload>>()
                .Publish(new GanttChartUpdatedPayload());
        }

        private void PublishArrowGraphSettingsUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<ArrowGraphSettingsUpdatedPayload>>()
                .Publish(new ArrowGraphSettingsUpdatedPayload());
        }

        private void PublishGraphCompilationUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompilationUpdatedPayload>>()
                .Publish(new GraphCompilationUpdatedPayload());
        }

        private static async Task<ProjectImportModel> ImportMicrosoftProjectAsync(string filename)
        {
            return await Task.Run(() => ImportMicrosoftProject(filename)).ConfigureAwait(true);
        }

        private static ProjectImportModel ImportMicrosoftProject(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }
            var xDoc = new XmlDocument();
            var nsMan = new XmlNamespaceManager(xDoc.NameTable);
            nsMan.AddNamespace("ns", "http://schemas.microsoft.com/project");
            xDoc.Load(filename);

            DateTime projectStart = XmlConvert.ToDateTime(xDoc[@"Project"][@"StartDate"].InnerText, XmlDateTimeSerializationMode.Local);
            int minutesPerDay = XmlConvert.ToInt32(xDoc[@"Project"][@"MinutesPerDay"].InnerText);
            double hoursPerDay = new TimeSpan(0, minutesPerDay, 0).TotalHours;

            // Resources.
            var resources = new List<ResourceModel>();
            var resourceUidToIdLookup = new Dictionary<int, int>();

            foreach (XmlNode projectResource in xDoc[@"Project"][@"Resources"].ChildNodes)
            {
                int resourceUid = XmlConvert.ToInt32(projectResource[@"UID"].InnerText);
                int resourceId = XmlConvert.ToInt32(projectResource[@"ID"].InnerText);
                if (resourceId == 0)
                {
                    continue;
                }
                string name = projectResource[@"Name"].InnerText;
                var resource = new ResourceModel
                {
                    Id = resourceId,
                    IsExplicitTarget = true,
                    Name = name,
                    DisplayOrder = resourceId,
                    UnitCost = XmlConvert.ToDouble(projectResource[@"Cost"].InnerText),
                    ColorFormat = new ColorFormatModel()
                };
                resources.Add(resource);

                resourceUidToIdLookup.Add(resourceUid, resourceId);
            }

            // Tasks.
            XmlNodeList projectTasks = xDoc[@"Project"][@"Tasks"].ChildNodes;

            var taskUidToIdLookup = new Dictionary<int, int>();

            foreach (XmlNode projectTask in projectTasks)
            {
                int taskUid = XmlConvert.ToInt32(projectTask[@"UID"].InnerText);
                int taskId = XmlConvert.ToInt32(projectTask[@"ID"].InnerText);
                taskUidToIdLookup.Add(taskUid, taskId);
            }

            // Resource assignments.
            var resourceAssignmentLookup = new Dictionary<int, IList<int>>();
            XmlNodeList projectAssignments = xDoc[@"Project"][@"Assignments"].ChildNodes;

            foreach (XmlNode projectAssignment in projectAssignments)
            {
                int taskUid = XmlConvert.ToInt32(projectAssignment[@"TaskUID"].InnerText);
                int resourceUid = XmlConvert.ToInt32(projectAssignment[@"ResourceUID"].InnerText);

                if (taskUidToIdLookup.TryGetValue(taskUid, out int taskId)
                    && resourceUidToIdLookup.TryGetValue(resourceUid, out int resourceId))
                {
                    if (!resourceAssignmentLookup.TryGetValue(taskId, out IList<int> resourceAssignments))
                    {
                        resourceAssignments = new List<int>();
                        resourceAssignmentLookup.Add(taskId, resourceAssignments);
                    }

                    resourceAssignments.Add(resourceId);
                }
            }

            // Cycle through tasks.
            var dependentActivities = new List<DependentActivityModel>();
            foreach (XmlNode projectTask in projectTasks)
            {
                int taskId = XmlConvert.ToInt32(projectTask[@"ID"].InnerText);
                if (taskId == 0)
                {
                    continue;
                }
                string name = projectTask[@"Name"].InnerText;
                double totalDurationMinutes = XmlConvert.ToTimeSpan(projectTask[@"Duration"].InnerText).TotalMinutes;
                int durationDays = Convert.ToInt32(totalDurationMinutes / minutesPerDay);
                DateTime? minimumEarliestStartDateTime = null;
                if (string.Equals(projectTask[@"ConstraintType"].InnerText, @"4", StringComparison.OrdinalIgnoreCase)) // START_NO_EARLIER_THAN
                {
                    minimumEarliestStartDateTime = XmlConvert.ToDateTime(projectTask[@"ConstraintDate"].InnerText, XmlDateTimeSerializationMode.Local);
                }

                if (!resourceAssignmentLookup.TryGetValue(taskId, out IList<int> targetResources))
                {
                    targetResources = new List<int>();
                }

                // Do not forget namespaces.
                // https://stackoverflow.com/questions/33125519/how-to-get-text-from-ms-projects-xml-file-in-c
                var dependencies = new List<int>();
                foreach (XmlNode predecessorNode in projectTask.SelectNodes("./ns:PredecessorLink", nsMan))
                {
                    int predecessorUid = XmlConvert.ToInt32(predecessorNode[@"PredecessorUID"].InnerText);

                    if (taskUidToIdLookup.TryGetValue(predecessorUid, out int predecessorId))
                    {
                        dependencies.Add(predecessorId);
                    }
                }

                var dependentActivity = new DependentActivityModel
                {
                    Activity = new ActivityModel
                    {
                        Id = taskId,
                        Name = name,
                        TargetResources = targetResources.ToList(),
                        Duration = durationDays,
                        MinimumEarliestStartDateTime = minimumEarliestStartDateTime,
                        AllocatedToResources = new List<int>(),
                    },
                    Dependencies = dependencies,
                    ResourceDependencies = new List<int>(),
                };
                dependentActivities.Add(dependentActivity);
            }

            return new ProjectImportModel
            {
                ProjectStart = projectStart,
                DependentActivities = dependentActivities.ToList(),
                Resources = resources.ToList()
            };
        }

        private static async Task<ProjectImportModel> ImportProjectJsonAsync(string filename)
        {
            return await Task.Run(() => ImportProjectJson(filename)).ConfigureAwait(true);
        }

        private static ProjectImportModel ImportProjectJson(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }

            var json = File.ReadAllText(filename);

            var model = JsonConvert.DeserializeObject<ProjectImportModel>(json);
            foreach (var dependentActivity in model.DependentActivities)
            {
                dependentActivity.ResourceDependencies = new List<int>();
                dependentActivity.Activity.AllocatedToResources = new List<int>();
            }

            return model;
        }

        private void ProcessProjectImportModel(ProjectImportModel importModel)
        {
            if (importModel == null)
            {
                throw new ArgumentNullException(nameof(importModel));
            }
            lock (m_Lock)
            {
                ResetProject();

                // Project Start Date.
                ProjectStartWithoutPublishing = importModel.ProjectStart;

                // Resources.
                foreach (ResourceModel resource in importModel.Resources)
                {
                    ResourceSettings.Resources.Add(resource);
                }
                //SetTargetResources();

                // Activities.
                m_CoreViewModel.AddManagedActivities(new HashSet<DependentActivityModel>(importModel.DependentActivities));

                HasStaleOutputs = true;
                IsProjectUpdated = true;

                m_CoreViewModel.ClearUndoStack();
                m_CoreViewModel.ClearRedoStack();
            }
        }

        private void ProcessProjectPlan(ProjectPlanModel projectPlan)
        {
            if (projectPlan == null)
            {
                throw new ArgumentNullException(nameof(projectPlan));
            }
            lock (m_Lock)
            {
                ResetProject();

                // Project Start Date.
                ProjectStartWithoutPublishing = projectPlan.ProjectStart;

                // Resources.
                m_CoreViewModel.UpdateResourceSettings(projectPlan.ResourceSettings);

                // Compilation.
                GraphCompilation = m_Mapper.Map<GraphCompilation<int, int, DependentActivity<int, int>>>(projectPlan.GraphCompilation);

                CyclomaticComplexity = projectPlan.GraphCompilation.CyclomaticComplexity;
                Duration = projectPlan.GraphCompilation.Duration;
                DurationManMonths = m_CoreViewModel.CalculateDurationManMonths();

                // Activities.
                // Be sure to do this after the resources and project start date have been added.
                m_CoreViewModel.AddManagedActivities(new HashSet<DependentActivityModel>(projectPlan.DependentActivities));

                m_CoreViewModel.UpdateActivitiesAllocatedToResources();

                m_CoreViewModel.CalculateResourceSeriesSet();

                m_CoreViewModel.SetCompilationOutput();

                m_CoreViewModel.CalculateCosts();

                // Arrow Graph.
                m_CoreViewModel.UpdateArrowGraphSettings(projectPlan.ArrowGraphSettings);
                ArrowGraph = projectPlan.ArrowGraph;

                HasStaleOutputs = projectPlan.HasStaleOutputs;
                IsProjectUpdated = false;

                m_CoreViewModel.ClearUndoStack();
                m_CoreViewModel.ClearRedoStack();
            }

            PublishGraphCompilationUpdatedPayload();
            PublishArrowGraphUpdatedPayload();
            PublishGanttChartUpdatedPayload();
        }

        private async Task<ProjectPlanModel> BuildProjectPlanAsync()
        {
            return await Task.Run(() => BuildProjectPlan()).ConfigureAwait(true);
        }

        private ProjectPlanModel BuildProjectPlan()
        {
            lock (m_Lock)
            {
                var graphCompilation = m_Mapper.Map<IGraphCompilation<int, int, IDependentActivity<int, int>>, GraphCompilationModel>(GraphCompilation);
                graphCompilation.CyclomaticComplexity = CyclomaticComplexity.GetValueOrDefault();
                graphCompilation.Duration = Duration.GetValueOrDefault();

                return new ProjectPlanModel
                {
                    ProjectStart = ProjectStart,
                    DependentActivities = m_Mapper.Map<List<DependentActivityModel>>(Activities),
                    ResourceSettings = ResourceSettings.CloneObject(),
                    ArrowGraphSettings = ArrowGraphSettings.CloneObject(),
                    GraphCompilation = graphCompilation,
                    ArrowGraph = ArrowGraph != null ? ArrowGraph.CloneObject() : new ArrowGraphModel() { Edges = new List<ActivityEdgeModel>(), Nodes = new List<EventNodeModel>(), IsStale = false },
                    HasStaleOutputs = HasStaleOutputs
                };
            }
        }

        private static async Task<ProjectPlanModel> OpenProjectPlanAsync(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }
            return await OpenSave.OpenProjectPlanAsync(filename).ConfigureAwait(true);
        }

        private static Task SaveProjectPlanAsync(
            ProjectPlanModel projectPlan,
            string filename)
        {
            if (projectPlan == null)
            {
                throw new ArgumentNullException(nameof(projectPlan));
            }
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }
            return Task.Run(() => OpenSave.SaveProjectPlan(projectPlan, filename));
        }

        private async Task<int> RunCalculateResourcedCyclomaticComplexityAsync()
        {
            return await Task.Run(() => m_CoreViewModel.RunCalculateResourcedCyclomaticComplexity()).ConfigureAwait(true);
        }

        private async Task RunCompileAsync()
        {
            await Task.Run(() => m_CoreViewModel.RunCompile()).ConfigureAwait(true);
        }

        private async Task RunTransitiveReductionAsync()
        {
            await Task.Run(() =>
            {
                m_CoreViewModel.RecordRedoUndo(() =>
                {
                    m_CoreViewModel.RunTransitiveReduction();
                });
            }).ConfigureAwait(true);
        }

        private async Task RunAutoCompileAsync()
        {
            await Task.Run(() => m_CoreViewModel.RunAutoCompile()).ConfigureAwait(true);
        }

        private void ResetProject()
        {
            lock (m_Lock)
            {
                // TODO
                m_CoreViewModel.ClearManagedActivities();
                //SelectedActivities.Clear();

                m_CoreViewModel.ClearSettings();

                GraphCompilation = new GraphCompilation<int, int, DependentActivity<int, int>>(
                    Enumerable.Empty<DependentActivity<int, int>>(),
                    Enumerable.Empty<IResourceSchedule<int, int>>());

                CyclomaticComplexity = null;
                Duration = null;

                m_CoreViewModel.ClearResourceSeriesSet();
                m_CoreViewModel.ClearCosts();

                HasCompilationErrors = false;
                m_CoreViewModel.SetCompilationOutput();

                ArrowGraph = null;

                ProjectStartWithoutPublishing = DateTime.UtcNow.BeginningOfDay();
                IsProjectUpdated = false;
                m_SettingService.Reset();

                HasStaleOutputs = false;

                m_CoreViewModel.ClearUndoStack();
                m_CoreViewModel.ClearRedoStack();
            }

            PublishGraphCompilationUpdatedPayload();
            PublishArrowGraphUpdatedPayload();
            PublishGanttChartUpdatedPayload();
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

        public async Task DoNewProjectPlanFileAsync()
        {
            var projectPlan = new ProjectPlanModel
            {
                Version = Versions.v0_2_1,
                ProjectStart = DateTime.Now.Date,
                DependentActivities = new List<DependentActivityModel>(),
                ArrowGraphSettings = m_SettingService.DefaultArrowGraphSettings,
                ResourceSettings = m_SettingService.DefaultResourceSettings,
                GraphCompilation = new GraphCompilationModel
                {
                    DependentActivities = new List<DependentActivityModel>(),
                    CyclomaticComplexity = 0,
                    Duration = 0,
                    ResourceSchedules = new List<ResourceScheduleModel>(),
                    Errors = new GraphCompilationErrorsModel
                    {
                        AllResourcesExplicitTargetsButNotAllActivitiesTargeted = false,
                        CircularDependencies = new List<CircularDependencyModel>(),
                        InvalidConstraints = new List<int>(),
                        MissingDependencies = new List<int>()
                    }
                },
                ArrowGraph = new ArrowGraphModel
                {
                    Edges = new List<ActivityEdgeModel>(),
                    Nodes = new List<EventNodeModel>(),
                    IsStale = false
                },
                HasStaleOutputs = false
            };
            ProcessProjectPlan(projectPlan);
            m_SettingService.SetFilePath("New Project");
        }

        public async Task DoOpenProjectPlanFileAsync()
        {
            await DoOpenProjectPlanFileAsync(string.Empty).ConfigureAwait(true);
        }

        public async Task DoSaveProjectPlanFileAsync()
        {
            string projectTitle = m_SettingService.PlanTitle;
            if (string.IsNullOrEmpty(projectTitle))
            {
                await DoSaveAsProjectPlanFileAsync().ConfigureAwait(true);
            }
            else
            {
                await DoSaveProjectPlanFileAsync(projectTitle).ConfigureAwait(true);
            }
        }

        public async Task DoSaveProjectPlanFileAsync(string projectTitle)
        {
            if (string.IsNullOrWhiteSpace(projectTitle))
            {
                throw new ArgumentNullException(nameof(projectTitle));
            }
            try
            {
                IsBusy = true;
                string directory = m_SettingService.PlanDirectory;
                string filename = Path.Combine(directory, projectTitle);
                filename = Path.ChangeExtension(filename, Resource.ProjectPlan.Filters.SaveProjectPlanFileExtension);
                ProjectPlanModel projectPlan = await BuildProjectPlanAsync().ConfigureAwait(true);
                await SaveProjectPlanAsync(projectPlan, filename).ConfigureAwait(true);
                IsProjectUpdated = false;
                m_SettingService.SetFilePath(filename);
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

        public async Task DoSaveAsProjectPlanFileAsync()
        {
            try
            {
                IsBusy = true;
                string directory = m_SettingService.PlanDirectory;

                var filter = new FileDialogFileTypeFilter(
                    Resource.ProjectPlan.Filters.SaveProjectPlanFileType,
                    Resource.ProjectPlan.Filters.SaveProjectPlanFileExtension
                    );

                bool result = m_FileDialogService.ShowSaveDialog(directory, filter);

                if (result)
                {
                    string filename = m_FileDialogService.Filename;
                    if (string.IsNullOrWhiteSpace(filename))
                    {
                        DispatchNotification(
                            Resource.ProjectPlan.Resources.Title_Error,
                            Resource.ProjectPlan.Resources.Message_EmptyFilename);
                    }
                    else
                    {
                        ProjectPlanModel projectPlan = await BuildProjectPlanAsync().ConfigureAwait(true);
                        await SaveProjectPlanAsync(projectPlan, filename).ConfigureAwait(true);
                        IsProjectUpdated = false;
                        m_SettingService.SetFilePath(filename);
                    }
                }
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

        public async Task DoImportProjectAsync()
        {
            try
            {
                IsBusy = true;
                if (IsProjectUpdated)
                {
                    var confirmation = new Confirmation

                    {
                        Title = Resource.ProjectPlan.Resources.Title_UnsavedChanges,
                        Content = Resource.ProjectPlan.Resources.Message_UnsavedChanges
                    };
                    m_ConfirmationInteractionRequest.Raise(confirmation);
                    if (!confirmation.Confirmed)
                    {
                        return;
                    }
                }
                string directory = m_SettingService.PlanDirectory;

                var filter = new FileDialogFileTypeFilter(
                    Resource.ProjectPlan.Filters.ImportMicrosoftProjectXMLFileType,
                    Resource.ProjectPlan.Filters.ImportMicrosoftProjectXMLFileExtension,
                    Resource.ProjectPlan.Filters.ImportProjectJsonFileType,
                    Resource.ProjectPlan.Filters.ImportProjectJsonFileExtension
                    );

                bool result = m_FileDialogService.ShowOpenDialog(directory, filter);

                if (result)
                {
                    string filename = m_FileDialogService.Filename;
                    if (string.IsNullOrWhiteSpace(filename))
                    {
                        DispatchNotification(
                            Resource.ProjectPlan.Resources.Title_Error,
                            Resource.ProjectPlan.Resources.Message_EmptyFilename);
                    }
                    else
                    {
                        var fileInfo = new FileInfo(filename);
                        var importModel = default(ProjectImportModel);

                        if (fileInfo.Extension == Resource.ProjectPlan.Filters.ImportMicrosoftProjectXMLFileExtension)
                        {
                            importModel = await ImportMicrosoftProjectAsync(filename).ConfigureAwait(true);
                        }
                        else
                        {
                            importModel = await ImportProjectJsonAsync(filename).ConfigureAwait(true);
                        }
                        ProcessProjectImportModel(importModel);

                        m_SettingService.SetFilePath(filename);

                        await RunAutoCompileAsync().ConfigureAwait(true);
                    }
                }
            }
            catch (Exception ex)
            {
                DispatchNotification(
                    Resource.ProjectPlan.Resources.Title_Error,
                    ex.Message);
                ResetProject();
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
                    var confirmation = new Confirmation
                    {
                        Title = Resource.ProjectPlan.Resources.Title_UnsavedChanges,
                        Content = Resource.ProjectPlan.Resources.Message_UnsavedChanges
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
                    Resource.ProjectPlan.Resources.Title_Error,
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
                    var confirmation = new ResourceSettingsManagerConfirmation(ResourceSettings.CloneObject())
                    {
                        Title = Resource.ProjectPlan.Resources.Title_ResourceSettings
                    };
                    m_ResourceSettingsManagerInteractionRequest.Raise(confirmation);
                    if (!confirmation.Confirmed)
                    {
                        return;
                    }

                    m_CoreViewModel.RecordRedoUndo(() =>
                    {
                        m_CoreViewModel.UpdateResourceSettings(confirmation.ResourceSettings);
                    });
                }

                HasStaleOutputs = true;
                IsProjectUpdated = true;

                await RunAutoCompileAsync().ConfigureAwait(true);

                m_CoreViewModel.UpdateActivitiesAllocatedToResources();
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

        public void DoOpenArrowGraphSettings()
        {
            try
            {
                IsBusy = true;
                lock (m_Lock)
                {
                    var confirmation = new ArrowGraphSettingsManagerConfirmation(ArrowGraphSettings.CloneObject())
                    {
                        Title = Resource.ProjectPlan.Resources.Title_ArrowGraphSettings
                    };
                    m_ArrowGraphSettingsManagerInteractionRequest.Raise(confirmation);
                    if (!confirmation.Confirmed)
                    {
                        return;
                    }

                    m_CoreViewModel.RecordRedoUndo(() =>
                    {
                        m_CoreViewModel.UpdateArrowGraphSettings(confirmation.ArrowGraphSettings);
                    });
                }
                IsProjectUpdated = true;
                PublishArrowGraphSettingsUpdatedPayload();
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

        public async Task DoCalculateResourcedCyclomaticComplexityAsync()
        {
            try
            {
                IsBusy = true;
                int resourcedCyclomaticComplexity = await RunCalculateResourcedCyclomaticComplexityAsync().ConfigureAwait(true);
                DispatchNotification(
                    Resource.ProjectPlan.Resources.Title_ResourcedCyclomaticComplexity,
                    $@"{Resource.ProjectPlan.Resources.Message_ResourcedCyclomaticComplexity}{Environment.NewLine}{Environment.NewLine}{resourcedCyclomaticComplexity}"
                );
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
            }
        }

        public async Task DoCompileAsync()
        {
            try
            {
                IsBusy = true;
                await RunCompileAsync().ConfigureAwait(true);
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

        public async Task DoTransitiveReductionAsync()
        {
            try
            {
                IsBusy = true;
                await RunTransitiveReductionAsync().ConfigureAwait(true);
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

        public void DoOpenHyperLink(string hyperlink)
        {
            if (string.IsNullOrWhiteSpace(hyperlink))
            {
                throw new ArgumentNullException(nameof(hyperlink));
            }
            try
            {
                IsBusy = true;
                var uri = new Uri(hyperlink);
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true,
                });
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

        public void DoOpenAbout()
        {
            try
            {
                IsBusy = true;
                m_AboutInteractionRequest.Raise(new Notification { Title = Resource.ProjectPlan.Resources.Title_AppName });
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

        #region IMainViewModel Members

        public IInteractionRequest NotificationInteractionRequest => m_NotificationInteractionRequest;

        public IInteractionRequest ConfirmationInteractionRequest => m_ConfirmationInteractionRequest;

        public IInteractionRequest ResourceSettingsManagerInteractionRequest => m_ResourceSettingsManagerInteractionRequest;

        public IInteractionRequest ArrowGraphSettingsManagerInteractionRequest => m_ArrowGraphSettingsManagerInteractionRequest;

        public IInteractionRequest AboutInteractionRequest => m_AboutInteractionRequest;

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

        public string Title
        {
            get
            {
                var titleBuilder = new StringBuilder();

                if (IsProjectUpdated)
                {
                    titleBuilder.Append(@"*");
                }

                if (string.IsNullOrWhiteSpace(m_SettingService.PlanTitle))
                {
                    titleBuilder.Append(Resource.ProjectPlan.Resources.Label_EmptyProjectTitle);
                }
                else
                {
                    titleBuilder.Append(m_SettingService.PlanTitle);
                }

                titleBuilder.Append($@" - {Resource.ProjectPlan.Resources.Title_ProjectPlan}");
                return titleBuilder.ToString();
            }
        }

        public bool IsProjectUpdated
        {
            get
            {
                return m_CoreViewModel.IsProjectUpdated;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.IsProjectUpdated = value;
                }
                RaisePropertyChanged();
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
                    m_CoreViewModel.RecordRedoUndo(() =>
                    {
                        m_CoreViewModel.ShowDates = value;
                    });
                }
                PublishShowDatesUpdatedPayload();
                RaisePropertyChanged();
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
                return m_CoreViewModel.AutoCompile;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.AutoCompile = value;
                }
                RaisePropertyChanged();
            }
        }

        public ArrowGraphSettingsModel ArrowGraphSettings
        {
            get
            {
                return m_CoreViewModel.ArrowGraphSettings;
            }
        }

        public ResourceSettingsModel ResourceSettings
        {
            get
            {
                return m_CoreViewModel.ResourceSettings;
            }
        }

        public IApplicationCommands ApplicationCommands
        {
            get;
        }

        public ICommand NewProjectPlanFileCommand
        {
            get;
            private set;
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

        public ICommand SaveAsProjectPlanFileCommand
        {
            get;
            private set;
        }

        public ICommand ImportProjectCommand
        {
            get;
            private set;
        }

        public ICommand ImportProjectJsonCommand
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

        public ICommand ToggleShowDatesCommand
        {
            get;
            private set;
        }

        public ICommand ToggleUseBusinessDaysCommand
        {
            get;
            private set;
        }

        public ICommand CalculateResourcedCyclomaticComplexityCommand
        {
            get;
            private set;
        }

        public ICommand CompileCommand
        {
            get;
            private set;
        }

        public ICommand TransitiveReductionCommand
        {
            get;
            private set;
        }

        public ICommand OpenHyperLinkCommand
        {
            get;
            private set;
        }

        public ICommand OpenAboutCommand
        {
            get;
            private set;
        }

        public ICommand ExportScenariosCommand
        {
            get;
            private set;
        }

        public ICommand ExportCsvCommand
        {
            get;
            private set;
        }

        public async Task DoExportScenariosAsync()
        {
            var filename = $"{m_SettingService.PlanTitle}.results.csv";
            try
            {
                IsBusy = true;

                // todo: move string to resources
                if (m_CoreViewModel.Activities.Count == 0
                    || m_CoreViewModel.ResourceSettings.Resources.Count == 0)
                {
                    var context = new Notification
                    {
                        Title = Resource.ProjectPlan.Resources.Title_Error,
                        Content = "Unable to export scenarios for a Project Plan with no activities or no resources."
                    };
                    m_NotificationInteractionRequest.Raise(context);

                    return;
                }
                if (m_CoreViewModel.ResourceSettings.Resources.All(x => x.IsExplicitTarget))
                {
                    var context = new Notification
                    {
                        Title = Resource.ProjectPlan.Resources.Title_Error,
                        Content = "Unable to export scenarios for a Project Plan when all resources are Explicit Targets."
                    };
                    m_NotificationInteractionRequest.Raise(context);
                    return;
                }

                var filter = new FileDialogFileTypeFilter("Comma Separated Values", ".csv");
                var directory = m_SettingService.PlanDirectory;

                var original = m_CoreViewModel.ResourceSettings;
                var scenarios = ResourceScenarioBuilder.Build(ResourceSettings);

                var headers = new[]
                {
                    "ImplicitResourceCount",
                    "ActivityRisk",
                    "ActivityStdDevRisk",
                    "CriticalityRisk",
                    "FibonacciRisk",
                    "GeometricActivityRisk",
                    "GeometricCriticalityRisk",
                    "GeometricFibonacciRisk",
                    "CyclomaticComplexity",
                    "DurationMonths",
                    "DirectCost",
                    "IndirectCost",
                    "OtherCost",
                    "TotalCost",
                };

                var lines = new List<string>
                {
                    string.Join(",", headers)
                };

                foreach (var scenario in scenarios)
                {
                    m_CoreViewModel.UpdateResourceSettings(scenario);
                    m_CoreViewModel.RunTransitiveReduction();
                    ProcessProjectPlan(BuildProjectPlan());

                    var metrics = m_CoreViewModel.Metrics;

                    var values = new List<string>
                    {
                        $"{scenario.Resources.Count(x => !x.IsExplicitTarget)}",
                        $"{metrics.Activity:0.000}",
                        $"{metrics.ActivityStdDevCorrection:0.000}",
                        $"{metrics.Criticality:0.000}",
                        $"{metrics.Fibonacci:0.000}",
                        $"{metrics.GeometricActivity:0.000}",
                        $"{metrics.GeometricCriticality:0.000}",
                        $"{metrics.GeometricFibonacci:0.000}",
                        $"{m_CoreViewModel.CyclomaticComplexity}",
                        $"{m_CoreViewModel.DurationManMonths:#0.0}",
                        $"{m_CoreViewModel.DirectCost:#0.0}",
                        $"{m_CoreViewModel.IndirectCost:#0.0}",
                        $"{m_CoreViewModel.OtherCost:#0.0}",
                        $"{m_CoreViewModel.TotalCost:#0.0}",
                    };

                    lines.Add(string.Join(",", values));
                }

                var csv = string.Join(Environment.NewLine, lines);

                File.WriteAllText(Path.Combine(directory, filename), csv);

                IsBusy = false;
            }
            catch (Exception ex)
            {
                DispatchNotification(
                    Resource.ProjectPlan.Resources.Title_Error,
                    ex.Message);
                ResetProject();
            }
            finally
            {
                IsBusy = false;
                RaiseCanExecuteChangedAllCommands();
            }

            var complete = new Notification
            {
                Title = Resource.ProjectPlan.Resources.Title_AppName,
                Content = $"Exported scenarios to {filename}"
            };

            m_NotificationInteractionRequest.Raise(complete);
        }

        public async Task DoExportCsvAsync()
        {
            var filename = m_SettingService.PlanTitle + ".csv";
            var directory = m_SettingService.PlanDirectory;

            try
            {
                IsBusy = true;

                var headers = new[]
                {
                    "Id",
                    "Name",
                    "TargetResources",
                    "AllocatedResources",
                    "IsDummy",
                    "Duration",
                    "TotalSlack",
                    "FreeSlack",
                    "InterferingSlack",
                    "IsCritical",
                    "EarliestStartTime",
                    "LatestStartTime",
                    "EarliestFinishTime",
                    "LatestFinishTime",
                    "EarliestStartDateTime",
                    "LatestStartDateTime",
                    "EarliestFinishDateTime",
                    "LatestFinishDateTime",
                    "Dependencies",
                    "ResourceDependencies",
                };

                var lines = new List<string>
                {
                    string.Join(",", headers)
                };

                foreach (var activity in m_CoreViewModel.Activities)
                {
                    var values = new List<string>
                    {
                        $"{activity.Id}",
                        $"\"{activity.Name}\"",
                        $"\"{string.Join(",", activity.TargetResources)}\"",
                        $"\"{string.Join(",", activity.AllocatedToResourcesString)}\"",
                        $"{activity.IsDummy}",
                        $"{activity.Duration}",
                        $"{activity.TotalSlack}",
                        $"{activity.FreeSlack}",
                        $"{activity.InterferingSlack}",
                        $"{activity.IsCritical}",
                        $"{activity.EarliestStartTime}",
                        $"{activity.LatestStartTime}",
                        $"{activity.EarliestFinishTime}",
                        $"{activity.LatestFinishTime}",
                        $"{activity.EarliestStartDateTime}",
                        $"{activity.LatestStartDateTime}",
                        $"{activity.EarliestFinishDateTime}",
                        $"{activity.LatestFinishDateTime}",
                        $"\"{string.Join(",", activity.Dependencies)}\"",
                        $"\"{string.Join(",", activity.ResourceDependencies)}\"",
                    };

                    lines.Add(string.Join(",", values));
                }

                var csv = string.Join(Environment.NewLine, lines);

                File.WriteAllText(Path.Combine(directory, filename), csv);

                IsBusy = false;
            }
            catch (Exception ex)
            {
                DispatchNotification(
                    Resource.ProjectPlan.Resources.Title_Error,
                    ex.Message);
                ResetProject();
            }
            finally
            {
                IsBusy = false;
                RaiseCanExecuteChangedAllCommands();
            }

            var complete = new Notification
            {
                Title = Resource.ProjectPlan.Resources.Title_AppName,
                Content = $"Exported csv to {filename}"
            };

            m_NotificationInteractionRequest.Raise(complete);
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
                        Title = Resource.ProjectPlan.Resources.Title_UnsavedChanges,
                        Content = Resource.ProjectPlan.Resources.Message_UnsavedChanges
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
                    string directory = m_SettingService.PlanDirectory;

                    var filter = new FileDialogFileTypeFilter(
                        Resource.ProjectPlan.Filters.OpenProjectPlanFileType,
                        Resource.ProjectPlan.Filters.OpenProjectPlanFileExtension
                        );

                    bool result = m_FileDialogService.ShowOpenDialog(directory, filter);
                    if (result)
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
                        Resource.ProjectPlan.Resources.Title_Error,
                        Resource.ProjectPlan.Resources.Message_EmptyFilename);
                }
                else
                {
                    ProjectPlanModel projectPlan = await OpenProjectPlanAsync(filename).ConfigureAwait(true);
                    ProcessProjectPlan(projectPlan);
                    m_SettingService.SetFilePath(filename);
                }
            }
            catch (Exception ex)
            {
                DispatchNotification(
                    Resource.ProjectPlan.Resources.Title_Error,
                    ex.Message);
                ResetProject();
            }
            finally
            {
                IsBusy = false;
                RaiseCanExecuteChangedAllCommands();
            }
        }

        #endregion
    }
}
