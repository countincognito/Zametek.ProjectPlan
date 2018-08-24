using FluentDateTime;
using net.sf.mpxj.MpxjUtilities;
using net.sf.mpxj.reader;
using Prism.Commands;
using Prism.Events;
using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
        private bool m_IsBusy;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IFileDialogService m_FileDialogService;
        private readonly IProjectSettingService m_ProjectSettingService;
        private readonly IEventAggregator m_EventService;
        private readonly InteractionRequest<Notification> m_NotificationInteractionRequest;
        private readonly InteractionRequest<Confirmation> m_ConfirmationInteractionRequest;
        private readonly InteractionRequest<ResourceSettingsManagerConfirmation> m_ResourceSettingsManagerInteractionRequest;
        private readonly InteractionRequest<ArrowGraphSettingsManagerConfirmation> m_ArrowGraphSettingsManagerInteractionRequest;
        private readonly InteractionRequest<Notification> m_AboutInteractionRequest;

        #endregion

        #region Ctors

        public MainViewModel(
            ICoreViewModel coreViewModel,
            IFileDialogService fileDialogService,
            IProjectSettingService projectSettingService,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_FileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            m_ProjectSettingService = projectSettingService ?? throw new ArgumentNullException(nameof(projectSettingService));
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
            await DoImportMicrosoftProjectAsync();
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
            InternalCompileCommand.RaiseCanExecuteChanged();
            InternalTransitiveReductionCommand.RaiseCanExecuteChanged();
            InternalOpenHyperLinkCommand.RaiseCanExecuteChanged();
            InternalOpenAboutCommand.RaiseCanExecuteChanged();
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
            foreach (var resource in mpx.Resources.ToIEnumerable<net.sf.mpxj.Resource>())
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
            foreach (var task in mpx.Tasks.ToIEnumerable<net.sf.mpxj.Task>())
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

                m_CoreViewModel.UpdateActivitiesAllocatedResources();

                m_CoreViewModel.SetCompilationOutput();

                m_CoreViewModel.CalculateCosts();

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

                m_CoreViewModel.ClearCosts();

                ProjectStartWithoutPublishing = DateTime.UtcNow.BeginningOfDay();
                IsProjectUpdated = false;
                m_ProjectSettingService.Reset();

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
            string projectTitle = m_ProjectSettingService.PlanTitle;
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
                string directory = m_ProjectSettingService.PlanDirectory;
                string filename = Path.Combine(directory, projectTitle);
                filename = Path.ChangeExtension(filename, Properties.Resources.Filter_SaveProjectPlanFileExtension);
                ProjectPlanDto projectPlan = await BuildProjectPlanDtoAsync();
                await SaveProjectPlanDtoAsync(projectPlan, filename);
                IsProjectUpdated = false;
                m_ProjectSettingService.SetFilePath(filename);
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
                string directory = m_ProjectSettingService.PlanDirectory;
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
                        m_ProjectSettingService.SetFilePath(filename);
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
                string directory = m_ProjectSettingService.PlanDirectory;
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
                        m_ProjectSettingService.SetFilePath(filename);

                        await RunAutoCompileAsync();
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

                m_CoreViewModel.UpdateActivitiesAllocatedResources();
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
                m_AboutInteractionRequest.Raise(new Notification() { Title = Properties.Resources.Title_AppName });
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
                return m_IsBusy;
            }
            set
            {
                m_IsBusy = value;
                RaisePropertyChanged(nameof(IsBusy));
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

                if (string.IsNullOrWhiteSpace(m_ProjectSettingService.PlanTitle))
                {
                    titleBuilder.Append(Properties.Resources.Label_EmptyProjectTitle);
                }
                else
                {
                    titleBuilder.Append(m_ProjectSettingService.PlanTitle);
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
                    string directory = m_ProjectSettingService.PlanDirectory;
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
                    m_ProjectSettingService.SetFilePath(filename);
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
