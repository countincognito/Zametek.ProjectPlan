using FluentDateTime;
using net.sf.mpxj.MpxjUtilities;
using net.sf.mpxj.reader;
using Prism.Commands;
using Prism.Events;
using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class MainViewModel
        : PropertyChangedPubSubViewModel, IMainViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private string m_ProjectTitle;
        private bool m_IsBusy;

        private static string s_DefaultProjectTitle = Properties.Resources.Label_DefaultTitle;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IFileDialogService m_FileDialogService;
        private readonly IAppSettingService m_AppSettingService;
        private readonly IEventAggregator m_EventService;
        private readonly InteractionRequest<Notification> m_NotificationInteractionRequest;
        private readonly InteractionRequest<Confirmation> m_ConfirmationInteractionRequest;
        private readonly InteractionRequest<Confirmation> m_ProjectTitleInteractionRequest;
        private readonly InteractionRequest<ResourceSettingsManagerConfirmation> m_ResourceSettingsManagerInteractionRequest;
        private readonly InteractionRequest<ArrowGraphSettingsManagerConfirmation> m_ArrowGraphSettingsManagerInteractionRequest;

        #endregion

        #region Ctors

        public MainViewModel(
            ICoreViewModel coreViewModel,
            IFileDialogService fileDialogService,
            IAppSettingService appSettingService,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_FileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            m_AppSettingService = appSettingService ?? throw new ArgumentNullException(nameof(appSettingService));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            m_NotificationInteractionRequest = new InteractionRequest<Notification>();
            m_ConfirmationInteractionRequest = new InteractionRequest<Confirmation>();
            m_ProjectTitleInteractionRequest = new InteractionRequest<Confirmation>();
            m_ResourceSettingsManagerInteractionRequest = new InteractionRequest<ResourceSettingsManagerConfirmation>();
            m_ArrowGraphSettingsManagerInteractionRequest = new InteractionRequest<ArrowGraphSettingsManagerConfirmation>();

            ResetProject();

            ShowDates = false;
            UseBusinessDaysWithoutPublishing = true;
            AutoCompile = true;
            InitializeCommands();

            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.ProjectStart), nameof(ProjectStart), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.IsProjectUpdated), nameof(IsProjectUpdated), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.ShowDates), nameof(ShowDates), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.UseBusinessDays), nameof(UseBusinessDays), ThreadOption.BackgroundThread);
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

        private IList<ManagedActivityViewModel> Activities => m_CoreViewModel.Activities;

        private bool HasCompilationErrors
        {
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.HasCompilationErrors = value;
                }
            }
        }

        private GraphCompilation<int, IDependentActivity<int>> GraphCompilation
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
        }

        private void RaiseCanExecuteChangedAllCommands()
        {
            InternalOpenProjectPlanFileCommand.RaiseCanExecuteChanged();
            InternalSaveProjectPlanFileCommand.RaiseCanExecuteChanged();
            InternalImportMicrosoftProjectCommand.RaiseCanExecuteChanged();
            InternalCloseProjectCommand.RaiseCanExecuteChanged();
            InternalOpenResourceSettingsCommand.RaiseCanExecuteChanged();
            InternalOpenArrowGraphSettingsCommand.RaiseCanExecuteChanged();
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
            m_EventService.GetEvent<PubSubEvent<ArrowGraphDtoUpdatedPayload>>()
                .Publish(new ArrowGraphDtoUpdatedPayload());
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
                    ResourceSettingsDto.Resources.Add(resourceDto);
                }
                //SetTargetResources();

                // Activities.
                foreach (DependentActivityDto dependentActivityDto in microsoftProjectDto.DependentActivities)
                {
                    m_CoreViewModel.AddManagedActivity(DtoConverter.FromDto(dependentActivityDto));
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
                ResourceSettingsDto = projectPlanDto.ResourceSettings;

                // Compilation.
                GraphCompilation = DtoConverter.FromDto(projectPlanDto.GraphCompilation);

                CyclomaticComplexity = projectPlanDto.GraphCompilation.CyclomaticComplexity;
                Duration = projectPlanDto.GraphCompilation.Duration;

                // Activities.
                // Be sure to do this after the resources and project start date have been added.
                foreach (DependentActivityDto dependentActivityDto in projectPlanDto.DependentActivities)
                {
                    m_CoreViewModel.AddManagedActivity(DtoConverter.FromDto(dependentActivityDto));
                }

                m_CoreViewModel.SetCompilationOutput();

                // Arrow Graph.
                ArrowGraphSettingsDto = projectPlanDto.ArrowGraphSettings;
                ArrowGraphDto = projectPlanDto.ArrowGraph;

                HasStaleOutputs = projectPlanDto.HasStaleOutputs;
            }

            PublishGraphCompilationUpdatedPayload();
            PublishArrowGraphDtoUpdatedPayload();
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
                    DependentActivities = Activities.Select(x => DtoConverter.ToDto(x)).ToList(),
                    ResourceSettings = ResourceSettingsDto.Copy(),
                    ArrowGraphSettings = ArrowGraphSettingsDto.Copy(),
                    GraphCompilation = DtoConverter.ToDto(GraphCompilation, CyclomaticComplexity.GetValueOrDefault(), Duration.GetValueOrDefault()),
                    ArrowGraph = ArrowGraphDto != null ? ArrowGraphDto.Copy() : new ArrowGraphDto() { Edges = new List<ActivityEdgeDto>(), Nodes = new List<EventNodeDto>(), IsStale = false },
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

        private async Task RunCompileAsync()
        {
            await Task.Run(() => m_CoreViewModel.RunCompile());
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

                GraphCompilation = new GraphCompilation<int, IDependentActivity<int>>(
                    false,
                    Enumerable.Empty<CircularDependency<int>>(),
                    Enumerable.Empty<int>(),
                    Enumerable.Empty<IDependentActivity<int>>(),
                    Enumerable.Empty<IResourceSchedule<int>>());

                CyclomaticComplexity = null;
                Duration = null;

                HasCompilationErrors = false;
                m_CoreViewModel.SetCompilationOutput();

                ArrowGraphDto = null;

                ProjectStartWithoutPublishing = DateTime.UtcNow.BeginningOfDay();
                IsProjectUpdated = false;
                ProjectTitle = s_DefaultProjectTitle;

                HasStaleOutputs = false;
            }

            PublishGraphCompilationUpdatedPayload();
            PublishArrowGraphDtoUpdatedPayload();
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

                        await RunAutoCompileAsync();
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
                    var confirmation = new ResourceSettingsManagerConfirmation(ResourceSettingsDto.Copy())
                    {
                        Title = Properties.Resources.Title_ResourceSettings
                    };
                    m_ResourceSettingsManagerInteractionRequest.Raise(confirmation);
                    if (!confirmation.Confirmed)
                    {
                        return;
                    }
                    ResourceSettingsDto = confirmation.ResourceSettingsDto;
                    m_CoreViewModel.UpdateActivitiesTargetResources();
                }

                HasStaleOutputs = true;
                IsProjectUpdated = true;

                await RunAutoCompileAsync();
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

        #endregion

        #region IMainViewModel Members

        public IInteractionRequest NotificationInteractionRequest => m_NotificationInteractionRequest;

        public IInteractionRequest ConfirmationInteractionRequest => m_ConfirmationInteractionRequest;

        public IInteractionRequest ProjectTitleInteractionRequest => m_ProjectTitleInteractionRequest;

        public IInteractionRequest ResourceSettingsManagerInteractionRequest => m_ResourceSettingsManagerInteractionRequest;

        public IInteractionRequest ArrowGraphSettingsManagerInteractionRequest => m_ArrowGraphSettingsManagerInteractionRequest;

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

        public ResourceSettingsDto ResourceSettingsDto
        {
            get
            {
                return m_CoreViewModel.ResourceSettingsDto;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.ResourceSettingsDto = value;
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

        #endregion
    }
}
