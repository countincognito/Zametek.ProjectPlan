using AutoMapper;
using FluentDateTime;
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
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
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
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_FileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            m_SettingService = settingService ?? throw new ArgumentNullException(nameof(settingService));
            m_Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
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
                    m_CoreViewModel.ProjectStart = value;
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
                    m_CoreViewModel.UseBusinessDays = value;
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

        private DelegateCommandBase InternalOpenProjectPlanFileCommand
        {
            get;
            set;
        }

        private async void OpenProjectPlanFile()
        {
            await DoOpenProjectPlanFileAsync();
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
            return IsProjectUpdated;
        }

        private DelegateCommandBase InternalSaveAsProjectPlanFileCommand
        {
            get;
            set;
        }

        private async void SaveAsProjectPlanFile()
        {
            await DoSaveAsProjectPlanFileAsync();
        }

        private DelegateCommandBase InternalImportMicrosoftProjectCommand
        {
            get;
            set;
        }

        private async void ImportMicrosoftProject()
        {
            //await DoImportMicrosoftProjectAsync();
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
            await DoOpenResourceSettingsAsync();
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
            await DoCalculateResourcedCyclomaticComplexityAsync();
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
            await DoCompileAsync();
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
            await DoTransitiveReductionAsync();
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

        private void OpenAbout()
        {
            DoOpenAbout();
        }

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            OpenProjectPlanFileCommand =
                InternalOpenProjectPlanFileCommand =
                    new DelegateCommand(OpenProjectPlanFile);
            SaveProjectPlanFileCommand =
                InternalSaveProjectPlanFileCommand =
                    new DelegateCommand(SaveProjectPlanFile, CanSaveProjectPlanFile);
            SaveAsProjectPlanFileCommand =
                InternalSaveAsProjectPlanFileCommand =
                    new DelegateCommand(SaveAsProjectPlanFile);
            ImportMicrosoftProjectCommand =
                InternalImportMicrosoftProjectCommand =
                    new DelegateCommand(ImportMicrosoftProject);
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
        }

        private void RaiseCanExecuteChangedAllCommands()
        {
            InternalOpenProjectPlanFileCommand.RaiseCanExecuteChanged();
            InternalSaveProjectPlanFileCommand.RaiseCanExecuteChanged();
            InternalSaveAsProjectPlanFileCommand.RaiseCanExecuteChanged();
            InternalImportMicrosoftProjectCommand.RaiseCanExecuteChanged();
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
                                    Title = Properties.Resources.Title_UnsavedChanges,
                                    Content = Properties.Resources.Message_UnsavedChanges
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
                                Properties.Resources.Title_Error,
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

        private void PublishArrowGraphDtoUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<ArrowGraphUpdatedPayload>>()
                .Publish(new ArrowGraphUpdatedPayload());
        }

        private void PublishGanttChartDtoUpdatedPayload()
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

        //private async Task<MicrosoftProjectDto> ImportMicrosoftProjectAsync(string filename)
        //{
        //    return await Task.Run(() => ImportMicrosoftProject(filename));
        //}

        //private static MicrosoftProjectDto ImportMicrosoftProject(string filename)
        //{
        //    if (string.IsNullOrWhiteSpace(filename))
        //    {
        //        throw new ArgumentNullException(nameof(filename));
        //    }
        //    ProjectReader reader = ProjectReaderUtility.getProjectReader(filename);
        //    net.sf.mpxj.ProjectFile mpx = reader.read(filename);
        //    DateTime projectStart = mpx.ProjectProperties.StartDate.ToDateTime();

        //    var resources = new List<Common.Project.v0_1_0.ResourceDto>();
        //    foreach (var resource in mpx.Resources.ToIEnumerable<net.sf.mpxj.Resource>())
        //    {
        //        int id = resource.ID.intValue();
        //        if (id == 0)
        //        {
        //            continue;
        //        }
        //        string name = resource.Name;
        //        var resourceDto = new Common.Project.v0_1_0.ResourceDto
        //        {
        //            Id = id,
        //            IsExplicitTarget = true,
        //            Name = name,
        //            DisplayOrder = id,
        //            ColorFormat = new Common.Project.v0_1_0.ColorFormatDto()
        //        };
        //        resources.Add(resourceDto);
        //    }

        //    var dependentActivities = new List<Common.Project.v0_1_0.DependentActivityDto>();
        //    foreach (var task in mpx.Tasks.ToIEnumerable<net.sf.mpxj.Task>())
        //    {
        //        int id = task.ID.intValue();
        //        if (id == 0)
        //        {
        //            continue;
        //        }
        //        string name = task.Name;
        //        int duration = Convert.ToInt32(task.Duration.Duration);

        //        DateTime? minimumEarliestStartDateTime = null;
        //        if (task.ConstraintType == net.sf.mpxj.ConstraintType.START_NO_EARLIER_THAN)
        //        {
        //            //minimumEarliestStartTime = Convert.ToInt32((task.ConstraintDate.ToDateTime() - projectStart).TotalDays);
        //            minimumEarliestStartDateTime = task.ConstraintDate.ToDateTime();
        //        }

        //        var targetResources = new List<int>();
        //        foreach (var resourceAssignment in task.ResourceAssignments.ToIEnumerable<net.sf.mpxj.ResourceAssignment>())
        //        {
        //            if (resourceAssignment.Resource != null)
        //            {
        //                targetResources.Add(resourceAssignment.Resource.ID.intValue());
        //            }
        //        }

        //        var dependencies = new List<int>();
        //        var preds = task.Predecessors;
        //        if (preds != null && !preds.isEmpty())
        //        {
        //            foreach (var pred in preds.ToIEnumerable<net.sf.mpxj.Relation>())
        //            {
        //                dependencies.Add(pred.TargetTask.ID.intValue());
        //            }
        //        }
        //        var dependentActivityDto = new Common.Project.v0_1_0.DependentActivityDto
        //        {
        //            Activity = new Common.Project.v0_1_0.ActivityDto
        //            {
        //                Id = id,
        //                Name = name,
        //                TargetResources = targetResources,
        //                Duration = duration,
        //                MinimumEarliestStartDateTime = minimumEarliestStartDateTime
        //            },
        //            Dependencies = dependencies,
        //            ResourceDependencies = new List<int>()
        //        };
        //        dependentActivities.Add(dependentActivityDto);
        //    }
        //    return new MicrosoftProjectDto
        //    {
        //        ProjectStart = projectStart,
        //        DependentActivities = dependentActivities.ToList(),
        //        Resources = resources.ToList()
        //    };
        //}

        //private void ProcessMicrosoftProject(MicrosoftProjectDto microsoftProjectDto)
        //{
        //    if (microsoftProjectDto == null)
        //    {
        //        throw new ArgumentNullException(nameof(microsoftProjectDto));
        //    }
        //    lock (m_Lock)
        //    {
        //        ResetProject();

        //        // Project Start Date.
        //        ProjectStartWithoutPublishing = microsoftProjectDto.ProjectStart;

        //        // Resources.
        //        foreach (Common.Project.v0_1_0.ResourceDto resourceDto in microsoftProjectDto.Resources)
        //        {
        //            ResourceSettingsDto.Resources.Add(resourceDto);
        //        }
        //        //SetTargetResources();

        //        // Activities.
        //        foreach (Common.Project.v0_1_0.DependentActivityDto dependentActivityDto in microsoftProjectDto.DependentActivities)
        //        {
        //            m_CoreViewModel.AddManagedActivity(Common.Project.v0_1_0.DtoConverter.FromDto(dependentActivityDto));
        //        }
        //    }
        //}

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
                ResourceSettings = projectPlan.ResourceSettings;

                // Compilation.
                GraphCompilation = m_Mapper.Map<GraphCompilation<int, int, DependentActivity<int, int>>>(projectPlan.GraphCompilation);

                CyclomaticComplexity = projectPlan.GraphCompilation.CyclomaticComplexity;
                Duration = projectPlan.GraphCompilation.Duration;

                // Activities.
                // Be sure to do this after the resources and project start date have been added.
                foreach (DependentActivityModel dependentActivity in projectPlan.DependentActivities)
                {
                    m_CoreViewModel.AddManagedActivity(m_Mapper.Map<DependentActivityModel, DependentActivity<int, int>>(dependentActivity));
                }

                m_CoreViewModel.UpdateActivitiesAllocatedToResources();

                m_CoreViewModel.SetCompilationOutput();

                m_CoreViewModel.CalculateCosts();

                // Arrow Graph.
                ArrowGraphSettings = projectPlan.ArrowGraphSettings;
                ArrowGraph = projectPlan.ArrowGraph;

                HasStaleOutputs = projectPlan.HasStaleOutputs;
            }

            PublishGraphCompilationUpdatedPayload();
            PublishArrowGraphDtoUpdatedPayload();
            PublishGanttChartDtoUpdatedPayload();
        }

        private async Task<ProjectPlanModel> BuildProjectPlanAsync()
        {
            return await Task.Run(() => BuildProjectPlan());
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
                throw new ArgumentException(nameof(filename));
            }
            return await OpenSave.OpenProjectPlanAsync(filename).ConfigureAwait(false);
        }

        private static Task SaveProjectPlanAsync(ProjectPlanModel projectPlan, string filename)
        {
            if (projectPlan == null)
            {
                throw new ArgumentNullException(nameof(projectPlan));
            }
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException(nameof(filename));
            }
            return Task.Run(() => OpenSave.SaveProjectPlan(projectPlan, filename));
        }

        private async Task<int> RunCalculateResourcedCyclomaticComplexityAsync()
        {
            return await Task.Run(() => m_CoreViewModel.RunCalculateResourcedCyclomaticComplexity());
        }

        private async Task RunCompileAsync()
        {
            await Task.Run(() => m_CoreViewModel.RunCompile());
        }

        private async Task RunTransitiveReductionAsync()
        {
            await Task.Run(() => m_CoreViewModel.RunTransitiveReduction());
        }

        private async Task RunAutoCompileAsync()
        {
            await Task.Run(() => m_CoreViewModel.RunAutoCompile());
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

                HasCompilationErrors = false;
                m_CoreViewModel.SetCompilationOutput();

                ArrowGraph = null;

                m_CoreViewModel.ClearCosts();

                ProjectStartWithoutPublishing = DateTime.UtcNow.BeginningOfDay();
                IsProjectUpdated = false;
                m_SettingService.Reset();

                HasStaleOutputs = false;
            }

            PublishGraphCompilationUpdatedPayload();
            PublishArrowGraphDtoUpdatedPayload();
            PublishGanttChartDtoUpdatedPayload();
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

        public async Task DoOpenProjectPlanFileAsync()
        {
            await DoOpenProjectPlanFileAsync(string.Empty);
        }

        public async Task DoSaveProjectPlanFileAsync()
        {
            string projectTitle = m_SettingService.PlanTitle;
            if (string.IsNullOrEmpty(projectTitle))
            {
                await DoSaveAsProjectPlanFileAsync();
            }
            else
            {
                await DoSaveProjectPlanFileAsync(projectTitle);
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
                filename = Path.ChangeExtension(filename, Properties.Resources.Filter_SaveProjectPlanFileExtension);
                ProjectPlanModel projectPlan = await BuildProjectPlanAsync();
                await SaveProjectPlanAsync(projectPlan, filename);
                IsProjectUpdated = false;
                m_SettingService.SetFilePath(filename);
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

        public async Task DoSaveAsProjectPlanFileAsync()
        {
            try
            {
                IsBusy = true;
                string directory = m_SettingService.PlanDirectory;

                bool result = m_FileDialogService.ShowSaveDialog(
                    directory,
                    Properties.Resources.Filter_SaveProjectPlanFileType,
                    Properties.Resources.Filter_SaveProjectPlanFileExtension);

                if (result)
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
                        ProjectPlanModel projectPlan = await BuildProjectPlanAsync();
                        await SaveProjectPlanAsync(projectPlan, filename);
                        IsProjectUpdated = false;
                        m_SettingService.SetFilePath(filename);
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

        //public async Task DoImportMicrosoftProjectAsync()
        //{
        //    try
        //    {
        //        IsBusy = true;
        //        if (IsProjectUpdated)
        //        {
        //            var confirmation = new Confirmation()
        //            {
        //                Title = Properties.Resources.Title_UnsavedChanges,
        //                Content = Properties.Resources.Message_UnsavedChanges
        //            };
        //            m_ConfirmationInteractionRequest.Raise(confirmation);
        //            if (!confirmation.Confirmed)
        //            {
        //                return;
        //            }
        //        }
        //        string directory = m_SettingService.PlanDirectory;
        //        if (m_FileDialogService.ShowOpenDialog(
        //            directory,
        //            Properties.Resources.Filter_ImportMicrosoftProjectFileType,
        //            Properties.Resources.Filter_ImportMicrosoftProjectFileExtension) == DialogResult.OK)
        //        {
        //            string filename = m_FileDialogService.Filename;
        //            if (string.IsNullOrWhiteSpace(filename))
        //            {
        //                DispatchNotification(
        //                    Properties.Resources.Title_Error,
        //                    Properties.Resources.Message_EmptyFilename);
        //            }
        //            else
        //            {
        //                MicrosoftProjectModel microsoftProjectDto = await ImportMicrosoftProjectAsync(filename);
        //                ProcessMicrosoftProject(microsoftProjectDto);

        //                HasStaleOutputs = true;
        //                IsProjectUpdated = true;
        //                m_SettingService.SetFilePath(filename);

        //                await RunAutoCompileAsync();
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        DispatchNotification(
        //            Properties.Resources.Title_Error,
        //            ex.Message);
        //        ResetProject();
        //    }
        //    finally
        //    {
        //        IsBusy = false;
        //        RaiseCanExecuteChangedAllCommands();
        //    }
        //}

        public void DoCloseProject()
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
                    var confirmation = new ResourceSettingsManagerConfirmation(ResourceSettings.CloneObject())
                    {
                        Title = Properties.Resources.Title_ResourceSettings
                    };
                    m_ResourceSettingsManagerInteractionRequest.Raise(confirmation);
                    if (!confirmation.Confirmed)
                    {
                        return;
                    }
                    ResourceSettings = confirmation.ResourceSettings;
                    m_CoreViewModel.UpdateActivitiesTargetResources();
                }

                HasStaleOutputs = true;
                IsProjectUpdated = true;

                await RunAutoCompileAsync();

                m_CoreViewModel.UpdateActivitiesAllocatedToResources();
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
                    var confirmation = new ArrowGraphSettingsManagerConfirmation(ArrowGraphSettings.CloneObject())
                    {
                        Title = Properties.Resources.Title_ArrowGraphSettings
                    };
                    m_ArrowGraphSettingsManagerInteractionRequest.Raise(confirmation);
                    if (!confirmation.Confirmed)
                    {
                        return;
                    }
                    ArrowGraphSettings = confirmation.ArrowGraphSettings;
                }
                IsProjectUpdated = true;
                PublishArrowGraphSettingsUpdatedPayload();
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

        public async Task DoCalculateResourcedCyclomaticComplexityAsync()
        {
            try
            {
                IsBusy = true;
                int resourcedCyclomaticComplexity = await RunCalculateResourcedCyclomaticComplexityAsync();
                DispatchNotification(
                    Properties.Resources.Title_ResourcedCyclomaticComplexity,
                    $@"{Properties.Resources.Message_ResourcedCyclomaticComplexity}{Environment.NewLine}{Environment.NewLine}{resourcedCyclomaticComplexity}"
                );
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

        public async Task DoTransitiveReductionAsync()
        {
            try
            {
                IsBusy = true;
                await RunTransitiveReductionAsync();
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
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
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

        public void DoOpenAbout()
        {
            try
            {
                IsBusy = true;
                m_AboutInteractionRequest.Raise(new Notification { Title = Properties.Resources.Title_AppName });
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
                    titleBuilder.Append(Properties.Resources.Label_EmptyProjectTitle);
                }
                else
                {
                    titleBuilder.Append(m_SettingService.PlanTitle);
                }

                titleBuilder.Append($@" - {Properties.Resources.Title_ProjectPlan}");
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
                    m_CoreViewModel.ShowDates = value;
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
            private set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.ArrowGraphSettings = value;
                }
            }
        }

        public ResourceSettingsModel ResourceSettings
        {
            get
            {
                return m_CoreViewModel.ResourceSettings;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.ResourceSettings = value;
                }
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

        public ICommand SaveAsProjectPlanFileCommand
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
                    string directory = m_SettingService.PlanDirectory;
                    bool result = m_FileDialogService.ShowOpenDialog(
                        directory,
                        Properties.Resources.Filter_OpenProjectPlanFileType,
                        Properties.Resources.Filter_OpenProjectPlanFileExtension);
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
                        Properties.Resources.Title_Error,
                        Properties.Resources.Message_EmptyFilename);
                }
                else
                {
                    ProjectPlanModel projectPlan = await OpenProjectPlanAsync(filename);
                    ProcessProjectPlan(projectPlan);
                    IsProjectUpdated = false;
                    m_SettingService.SetFilePath(filename);
                }
            }
            catch (Exception ex)
            {
                DispatchNotification(
                    Properties.Resources.Title_Error,
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
