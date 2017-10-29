using CsvHelper;
using FluentDateTime;
using net.sf.mpxj.MpxjUtilities;
using net.sf.mpxj.reader;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Prism.Commands;
using Prism.Events;
using Prism.Interactivity.InteractionRequest;
using Prism.Mvvm;
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
using System.Windows.Media.Imaging;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class MainViewModel
        : BindableBase, IMainViewModel, IActivitiesManagerViewModel, IArrowGraphManagerViewModel, IMetricsManagerViewModel, IResourceChartsManagerViewModel, IEarnedValueChartsManagerViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private readonly VertexGraphCompiler<int, IDependentActivity<int>> m_VertexGraphCompiler;
        private string m_ProjectTitle;
        private bool m_IsProjectUpdated;
        private DateTime m_ProjectStart;
        private bool m_ShowDates;
        private bool m_UseBusinessDays;
        private bool m_AutoCompile;
        private double? m_DirectCost;
        private double? m_IndirectCost;
        private double? m_OtherCost;
        private double? m_TotalCost;
        private bool m_IsBusy;
        private string m_CompilationOutput;
        private bool m_HasCompilationErrors;
        private bool m_HasStaleArrowGraph;
        private bool m_HasStaleOutputs;

        private IList<ResourceSeries> m_ResourceChartSeriesSet;
        private bool m_ExportResourceChartAsCosts;
        private PlotModel m_ResourceChartPlotModel;
        private int m_ResourceChartOutputWidth;
        private int m_ResourceChartOutputHeight;

        private IList<EarnedValuePoint> m_EarnedValueChartPointSet;
        private PlotModel m_EarnedValueChartPlotModel;
        private int m_EarnedValueChartOutputWidth;
        private int m_EarnedValueChartOutputHeight;

        private static string s_DefaultProjectTitle = Properties.Resources.Label_DefaultTitle;

        private double? m_CriticalityRisk;
        private double? m_FibonacciRisk;
        private double? m_ActivityRisk;
        private double? m_ActivityRiskWithStdDevCorrection;
        private double? m_GeometricCriticalityRisk;
        private double? m_GeometricFibonacciRisk;
        private double? m_GeometricActivityRisk;
        private int? m_CyclomaticComplexity;
        private double? m_DurationManMonths;

        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IProjectManager m_ProjectManager;
        private readonly ISettingManager m_SettingManager;
        private readonly IFileDialogService m_FileDialogService;
        private readonly IAppSettingService m_AppSettingService;
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
            IProjectManager projectManager,
            ISettingManager settingManager,
            IFileDialogService fileDialogService,
            IAppSettingService appSettingService,
            IEventAggregator eventService)
        {
            m_ProjectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
            m_SettingManager = settingManager ?? throw new ArgumentNullException(nameof(settingManager));
            m_FileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            m_AppSettingService = appSettingService ?? throw new ArgumentNullException(nameof(appSettingService));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            m_Lock = new object();
            m_VertexGraphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            m_NotificationInteractionRequest = new InteractionRequest<Notification>();
            m_ConfirmationInteractionRequest = new InteractionRequest<Confirmation>();
            m_ProjectTitleInteractionRequest = new InteractionRequest<Confirmation>();
            m_ResourceSettingsManagerInteractionRequest = new InteractionRequest<ResourceSettingsManagerConfirmation>();
            m_ArrowGraphSettingsManagerInteractionRequest = new InteractionRequest<ArrowGraphSettingsManagerConfirmation>();
            m_DateTimeCalculator = new DateTimeCalculator();
            Activities = new ObservableCollection<ManagedActivityViewModel>();
            SelectedActivities = new ObservableCollection<ManagedActivityViewModel>();
            ResourceDtos = new List<ResourceDto>();
            m_ResourceChartSeriesSet = new List<ResourceSeries>();
            ResourceChartPlotModel = null;
            ResourceChartOutputWidth = 1000;
            ResourceChartOutputHeight = 500;
            m_EarnedValueChartPointSet = new List<EarnedValuePoint>();
            EarnedValueChartPlotModel = null;
            EarnedValueChartOutputWidth = 1000;
            EarnedValueChartOutputHeight = 500;

            ResetProject();

            ShowDates = false;
            UseBusinessDaysWithoutPublishing = true;
            AutoCompile = true;
            InitializeCommands();
            SubscribeToEvents();
        }

        #endregion

        #region Properties

        public bool HasStaleOutputs
        {
            get
            {
                return m_HasStaleOutputs;
            }
            private set
            {
                m_HasStaleOutputs = value;
                if (m_HasStaleOutputs
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
                return m_ProjectStart;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ProjectStart = value;
                }
                IsProjectUpdated = true;
                RaisePropertyChanged(nameof(ProjectStart));
            }
        }

        public bool UseBusinessDaysWithoutPublishing
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
            get;
            private set;
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
                return m_HasCompilationErrors;
            }
            private set
            {
                m_HasCompilationErrors = value;
                RaisePropertyChanged(nameof(HasCompilationErrors));
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

        public IList<ResourceDto> ResourceDtos
        {
            get;
        }

        public MetricsDto MetricsDto
        {
            get;
            private set;
        }

        public IInteractionRequest NotificationInteractionRequest => m_NotificationInteractionRequest;

        public IInteractionRequest ConfirmationInteractionRequest => m_ConfirmationInteractionRequest;

        public IInteractionRequest ProjectTitleInteractionRequest => m_ProjectTitleInteractionRequest;

        public IInteractionRequest ResourceSettingsManagerInteractionRequest => m_ResourceSettingsManagerInteractionRequest;

        public IInteractionRequest ArrowGraphSettingsManagerInteractionRequest => m_ArrowGraphSettingsManagerInteractionRequest;

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

        private async void OpenArrowGraphSettings()
        {
            await DoOpenArrowGraphSettingsAsync();
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

        public DelegateCommandBase InternalCopyResourceChartToClipboardCommand
        {
            get;
            private set;
        }

        private void CopyResourceChartToClipboard()
        {
            lock (m_Lock)
            {
                if (CanCopyResourceChartToClipboard())
                {
                    var pngExporter = new OxyPlot.Wpf.PngExporter
                    {
                        Width = ResourceChartOutputWidth,
                        Height = ResourceChartOutputHeight,
                        Background = OxyColors.White
                    };
                    BitmapSource bitmap = pngExporter.ExportToBitmap(ResourceChartPlotModel);
                    System.Windows.Clipboard.SetImage(bitmap);
                }
            }
        }

        private bool CanCopyResourceChartToClipboard()
        {
            lock (m_Lock)
            {
                return ResourceChartPlotModel != null;
            }
        }

        public DelegateCommandBase InternalExportResourceChartToCsvCommand
        {
            get;
            private set;
        }

        private async void ExportResourceChartToCsv()
        {
            await DoExportResourceChartToCsvAsync();
        }

        private bool CanExportResourceChartToCsv()
        {
            lock (m_Lock)
            {
                return m_ResourceChartSeriesSet.Any();
            }
        }

        public DelegateCommandBase InternalCopyEarnedValueChartToClipboardCommand
        {
            get;
            private set;
        }

        private void CopyEarnedValueChartToClipboard()
        {
            lock (m_Lock)
            {
                if (CanCopyEarnedValueChartToClipboard())
                {
                    var pngExporter = new OxyPlot.Wpf.PngExporter
                    {
                        Width = EarnedValueChartOutputWidth,
                        Height = EarnedValueChartOutputHeight,
                        Background = OxyColors.White
                    };
                    BitmapSource bitmap = pngExporter.ExportToBitmap(EarnedValueChartPlotModel);
                    System.Windows.Clipboard.SetImage(bitmap);
                }
            }
        }

        private bool CanCopyEarnedValueChartToClipboard()
        {
            lock (m_Lock)
            {
                return EarnedValueChartPlotModel != null;
            }
        }

        public DelegateCommandBase InternalExportEarnedValueChartToCsvCommand
        {
            get;
            private set;
        }

        private async void ExportEarnedValueChartToCsv()
        {
            await DoExportEarnedValueChartToCsvAsync();
        }

        private bool CanExportEarnedValueChartToCsv()
        {
            lock (m_Lock)
            {
                return m_EarnedValueChartPointSet.Any();
            }
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
            CopyResourceChartToClipboardCommand =
                InternalCopyResourceChartToClipboardCommand =
                    new DelegateCommand(CopyResourceChartToClipboard, CanCopyResourceChartToClipboard);
            ExportResourceChartToCsvCommand =
                InternalExportResourceChartToCsvCommand =
                    new DelegateCommand(ExportResourceChartToCsv, CanExportResourceChartToCsv);
            CopyEarnedValueChartToClipboardCommand =
                InternalCopyEarnedValueChartToClipboardCommand =
                    new DelegateCommand(CopyEarnedValueChartToClipboard, CanCopyEarnedValueChartToClipboard);
            ExportEarnedValueChartToCsvCommand =
                InternalExportEarnedValueChartToCsvCommand =
                    new DelegateCommand(ExportEarnedValueChartToCsv, CanExportEarnedValueChartToCsv);
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
            InternalCopyResourceChartToClipboardCommand.RaiseCanExecuteChanged();
            InternalExportResourceChartToCsvCommand.RaiseCanExecuteChanged();
            InternalCopyEarnedValueChartToClipboardCommand.RaiseCanExecuteChanged();
            InternalExportEarnedValueChartToCsvCommand.RaiseCanExecuteChanged();
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
                IsProjectUpdated = true;
                if (ArrowGraphDto != null)
                {
                    HasStaleArrowGraph = true;
                }
                SetCompilationOutput();
                CalculateMetrics();
                CalculateGraphMetrics();

                SetResourceChartSeriesSet();
                SetResourceChartPlotModel();
                SetEarnedValueChartPointSet();
                SetEarnedValueChartPlotModel();
                CalculateCosts();
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
        }

        private void CalculateCosts()
        {
            lock (m_Lock)
            {
                ClearCostProperties();
                if (HasCompilationErrors)
                {
                    return;
                }
                IList<ResourceSeries> seriesSet = m_ResourceChartSeriesSet;
                if (seriesSet != null
                    && seriesSet.Any())
                {
                    DirectCost = seriesSet.Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Direct).Sum(x => x.Values.Sum(y => x.UnitCost));
                    IndirectCost = seriesSet.Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect).Sum(x => x.Values.Sum(y => x.UnitCost));
                    OtherCost = seriesSet.Where(x => x.InterActivityAllocationType == InterActivityAllocationType.None).Sum(x => x.Values.Sum(y => x.UnitCost));
                    TotalCost = seriesSet.Sum(x => x.Values.Sum(y => x.UnitCost));
                }
            }
        }

        private void ClearCostProperties()
        {
            lock (m_Lock)
            {
                DirectCost = null;
                IndirectCost = null;
                OtherCost = null;
                TotalCost = null;
            }
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
                        output.AppendLine($@"*** {FormatScheduleOutput(previousFinishTime)} -> {FormatScheduleOutput(startTime)} ***");
                    }
                    output.AppendLine(
                        $@"Activity {scheduledActivity.Id}: {FormatScheduleOutput(startTime)} -> {FormatScheduleOutput(finishTime)}");
                    previousFinishTime = finishTime;
                }
                output.AppendLine();
            }
            return output.ToString();
        }

        private string FormatScheduleOutput(int days)
        {
            lock (m_Lock)
            {
                if (ShowDates)
                {
                    return m_DateTimeCalculator.AddDays(ProjectStart, days).ToString("d");
                }
                return days.ToString();
            }
        }

        private async Task CalculateMetricsAsync()
        {
            await Task.Run(() => CalculateMetrics());
        }

        private void CalculateMetrics()
        {
            lock (m_Lock)
            {
                ClearMetricProperties();
                if (HasCompilationErrors)
                {
                    return;
                }
                MetricsDto = m_ProjectManager.CalculateProjectMetrics(
                    Activities.Where(x => !x.IsDummy).Select(x => (IActivity<int>)x.DependentActivity).ToList(),
                    ArrowGraphSettingsDto?.ActivitySeverities);
                SetMetricProperties();
            }
        }

        private void ClearMetricProperties()
        {
            lock (m_Lock)
            {
                CriticalityRisk = null;
                FibonacciRisk = null;
                ActivityRisk = null;
                ActivityRiskWithStdDevCorrection = null;
                GeometricCriticalityRisk = null;
                GeometricFibonacciRisk = null;
                GeometricActivityRisk = null;
            }
        }

        private void SetMetricProperties()
        {
            lock (m_Lock)
            {
                ClearMetricProperties();
                MetricsDto metricsDto = MetricsDto;
                if (metricsDto != null)
                {
                    CriticalityRisk = metricsDto.Criticality;
                    FibonacciRisk = metricsDto.Fibonacci;
                    ActivityRisk = metricsDto.Activity;
                    ActivityRiskWithStdDevCorrection = metricsDto.ActivityStdDevCorrection;
                    GeometricCriticalityRisk = metricsDto.GeometricCriticality;
                    GeometricFibonacciRisk = metricsDto.GeometricFibonacci;
                    GeometricActivityRisk = metricsDto.GeometricActivity;
                }
            }
        }

        private void CalculateGraphMetrics()
        {
            lock (m_Lock)
            {
                ClearGraphMetricProperties();
                if (HasCompilationErrors)
                {
                    return;
                }
                SetGraphMetricProperties();
            }
        }

        private void ClearGraphMetricProperties()
        {
            lock (m_Lock)
            {
                CyclomaticComplexity = null;
                DurationManMonths = null;
            }
        }

        private void SetGraphMetricProperties()
        {
            lock (m_Lock)
            {
                ClearGraphMetricProperties();
                CyclomaticComplexity = m_VertexGraphCompiler.CyclomaticComplexity;
                DurationManMonths = CalculateDurationManMonths();
            }
        }

        private double? CalculateDurationManMonths()
        {
            lock (m_Lock)
            {
                int durationManDays = m_VertexGraphCompiler.Duration;
                if (durationManDays == 0)
                {
                    return null;
                }
                int daysPerWeek = m_DateTimeCalculator.DaysPerWeek;
                return durationManDays / (daysPerWeek * 52.0 / 12.0);
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

                // Compilation.
                GraphCompilation = new GraphCompilation<int, IDependentActivity<int>>(
                    projectPlanDto.AllResourcesExplicitTargetsButNotAllActivitiesTargeted,
                    projectPlanDto.CircularDependencies.Select(x => DtoConverter.FromDto(x)),
                    projectPlanDto.MissingDependencies,
                    projectPlanDto.DependentActivities.Select(x => DtoConverter.FromDto(x)),
                    projectPlanDto.ResourceSchedules.Select(x => DtoConverter.FromDto(x)));

                SetCompilationOutput();
                CalculateMetrics();
                ClearGraphMetricProperties();

                SetResourceChartSeriesSet();
                SetResourceChartPlotModel();
                SetEarnedValueChartPointSet();
                SetEarnedValueChartPlotModel();
                CalculateCosts();

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

        private void SetResourceChartSeriesSet()
        {
            lock (m_Lock)
            {
                IList<IResourceSchedule<int>> resourceSchedules = GraphCompilation?.ResourceSchedules;
                var seriesSet = new List<ResourceSeries>();
                if (resourceSchedules != null
                    && resourceSchedules.Any())
                {
                    IDictionary<int, ColorFormatDto> colorFormatLookup = ResourceDtos.ToDictionary(x => x.Id, x => x.ColorFormat);
                    var indirectResourceIdsToIgnore = new HashSet<int>();
                    int finishTime = resourceSchedules.Max(x => x.FinishTime);
                    int spareResourceCount = 1;
                    var scheduledSeriesSet = new List<ResourceSeries>();
                    for (int resourceIndex = 0; resourceIndex < resourceSchedules.Count; resourceIndex++)
                    {
                        IResourceSchedule<int> resourceSchedule = resourceSchedules[resourceIndex];
                        var series = new ResourceSeries()
                        {
                            Values = resourceSchedule.ActivityAllocation.Select(x => x ? 1 : 0).ToList()
                        };
                        series.InterActivityAllocationType = InterActivityAllocationType.None;
                        var stringBuilder = new StringBuilder();
                        IResource<int> resource = resourceSchedule.Resource;
                        if (resource != null)
                        {
                            series.InterActivityAllocationType = resource.InterActivityAllocationType;
                            indirectResourceIdsToIgnore.Add(resource.Id);
                            if (string.IsNullOrWhiteSpace(resource.Name))
                            {
                                stringBuilder.Append($@"Resource {resource.Id}");
                            }
                            else
                            {
                                stringBuilder.Append($@"{resource.Name}");
                            }
                        }
                        else
                        {
                            stringBuilder.Append($@"Resource {spareResourceCount}");
                            spareResourceCount++;
                        }

                        series.Title = stringBuilder.ToString();
                        series.ColorFormatDto = resource != null && colorFormatLookup.ContainsKey(resource.Id) ? colorFormatLookup[resource.Id].Copy() : new ColorFormatDto();
                        series.UnitCost = resource?.UnitCost ?? 0;
                        series.DisplayOrder = resource?.DisplayOrder ?? 0;
                        scheduledSeriesSet.Add(series);
                    }

                    // Now add the remaining resources that are indirect costs, but
                    // sort them separately and add them to the front of the list.
                    var unscheduledSeriesSet = new List<ResourceSeries>();
                    IEnumerable<ResourceDto> indirectResources =
                        ResourceDtos.Where(x => !indirectResourceIdsToIgnore.Contains(x.Id) && x.InterActivityAllocationType == InterActivityAllocationType.Indirect);
                    foreach (ResourceDto resourceDto in indirectResources)
                    {
                        var series = new ResourceSeries()
                        {
                            InterActivityAllocationType = resourceDto.InterActivityAllocationType,
                            Values = new List<int>(Enumerable.Repeat(1, finishTime))
                        };
                        var stringBuilder = new StringBuilder();
                        if (string.IsNullOrWhiteSpace(resourceDto.Name))
                        {
                            stringBuilder.Append($@"Resource {resourceDto.Id}");
                        }
                        else
                        {
                            stringBuilder.Append($@"{resourceDto.Name}");
                        }

                        series.Title = stringBuilder.ToString();
                        series.ColorFormatDto = resourceDto.ColorFormat != null ? resourceDto.ColorFormat.Copy() : new ColorFormatDto();
                        series.UnitCost = resourceDto.UnitCost;
                        series.DisplayOrder = resourceDto.DisplayOrder;
                        unscheduledSeriesSet.Add(series);
                    }

                    seriesSet.AddRange(unscheduledSeriesSet.OrderBy(x => x.DisplayOrder));
                    seriesSet.AddRange(scheduledSeriesSet.OrderBy(x => x.DisplayOrder));
                }

                m_ResourceChartSeriesSet.Clear();
                foreach (ResourceSeries series in seriesSet)
                {
                    m_ResourceChartSeriesSet.Add(series);
                }
            }
        }

        private void SetResourceChartPlotModel()
        {
            lock (m_Lock)
            {
                IList<ResourceSeries> seriesSet = m_ResourceChartSeriesSet;
                PlotModel plotModel = null;
                if (seriesSet != null
                    && seriesSet.Any())
                {
                    plotModel = new PlotModel();
                    plotModel.Axes.Add(BuildResourceChartXAxis());
                    plotModel.Axes.Add(BuildResourceChartYAxis());
                    plotModel.LegendPlacement = LegendPlacement.Outside;
                    plotModel.LegendPosition = LegendPosition.RightMiddle;
                    var total = new List<int>();
                    foreach (ResourceSeries series in seriesSet)
                    {
                        if (series != null)
                        {
                            var areaSeries = new AreaSeries
                            {
                                Smooth = false,
                                StrokeThickness = 0.0,
                                Title = series.Title,
                                Color = OxyColor.FromArgb(
                                    series.ColorFormatDto.A,
                                    series.ColorFormatDto.R,
                                    series.ColorFormatDto.G,
                                    series.ColorFormatDto.B)
                            };
                            for (int i = 0; i < series.Values.Count; i++)
                            {
                                int j = series.Values[i];
                                if (i >= total.Count)
                                {
                                    total.Add(0);
                                }
                                areaSeries.Points.Add(new DataPoint(CalculateChartTimeXValue(i), total[i]));
                                total[i] += j;
                                areaSeries.Points2.Add(new DataPoint(CalculateChartTimeXValue(i), total[i]));
                            }
                            plotModel.Series.Add(areaSeries);
                        }
                    }
                }
                ResourceChartPlotModel = plotModel;
            }
        }

        private Axis BuildResourceChartXAxis()
        {
            lock (m_Lock)
            {
                IList<IResourceSchedule<int>> resourceSchedules = GraphCompilation?.ResourceSchedules;
                Axis axis = null;
                if (resourceSchedules != null
                    && resourceSchedules.Any())
                {
                    int finishTime = resourceSchedules.Max(x => x.FinishTime);
                    double minValue = CalculateChartTimeXValue(0);
                    double maxValue = CalculateChartTimeXValue(finishTime);
                    if (ShowDates)
                    {
                        axis = new DateTimeAxis
                        {
                            Position = AxisPosition.Bottom,
                            Minimum = minValue,
                            Maximum = maxValue,
                            Title = Properties.Resources.Label_TimeAxisTitle,
                            StringFormat = "d"
                        };
                    }
                    else
                    {
                        axis = new LinearAxis
                        {
                            Position = AxisPosition.Bottom,
                            Minimum = minValue,
                            Maximum = maxValue,
                            Title = Properties.Resources.Label_TimeAxisTitle
                        };
                    }
                }
                else
                {
                    axis = new LinearAxis();
                }
                return axis;
            }
        }

        private static Axis BuildResourceChartYAxis()
        {
            return new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = Properties.Resources.Label_ResourcesAxisTitle
            };
        }

        private Task<DataTable> BuildResourceChartDataTableAsync()
        {
            return Task.Run(() => BuildResourceChartDataTable());
        }

        private DataTable BuildResourceChartDataTable()
        {
            lock (m_Lock)
            {
                var table = new DataTable();
                IList<ResourceSeries> seriesSet = m_ResourceChartSeriesSet.OrderBy(x => x.DisplayOrder).ToList();
                if (seriesSet != null
                    && seriesSet.Any())
                {
                    table.Columns.Add(new DataColumn(Properties.Resources.Label_TimeAxisTitle));

                    // Create the column titles.
                    for (int seriesIndex = 0; seriesIndex < seriesSet.Count; seriesIndex++)
                    {
                        var column = new DataColumn(seriesSet[seriesIndex].Title, typeof(int));
                        table.Columns.Add(column);
                    }

                    // Pivot the series values.
                    int valueCount = seriesSet.Max(x => x.Values.Count);
                    for (int timeIndex = 0; timeIndex < valueCount; timeIndex++)
                    {
                        var rowData = new List<object>
                        {
                            FormatScheduleOutput(timeIndex)
                        };
                        rowData.AddRange(seriesSet.Select(x => x.Values[timeIndex] * (ExportResourceChartAsCosts ? x.UnitCost : 1)).Cast<object>());
                        table.Rows.Add(rowData.ToArray());
                    }
                }
                return table;
            }
        }

        private void SetEarnedValueChartPointSet()
        {
            lock (m_Lock)
            {
                IList<IDependentActivity<int>> dependentActivities =
                    GraphCompilation.DependentActivities
                    .Select(x => (IDependentActivity<int>)x.WorkingCopy())
                    .OrderBy(x => x.EarliestFinishTime.GetValueOrDefault())
                    .ThenBy(x => x.EarliestStartTime.GetValueOrDefault())
                    .ToList();
                var pointSet = new List<EarnedValuePoint>();
                if (!HasCompilationErrors
                    && dependentActivities.Any()
                    && dependentActivities.All(x => x.EarliestFinishTime.HasValue))
                {
                    pointSet.Add(new EarnedValuePoint
                    {
                        Time = 0,
                        ActivityId = string.Empty,
                        ActivityName = string.Empty,
                        EarnedValue = 0,
                        EarnedValuePercentage = 0.0
                    });

                    double totalTime = Convert.ToDouble(dependentActivities.Sum(s => s.Duration));
                    int runningTotal = 0;
                    foreach (IDependentActivity<int> activity in dependentActivities)
                    {
                        runningTotal += activity.Duration;
                        double percentage = (runningTotal / totalTime) * 100.0;
                        int time = activity.EarliestFinishTime.GetValueOrDefault();
                        pointSet.Add(new EarnedValuePoint
                        {
                            Time = time,
                            ActivityId = activity.Id.ToString(),
                            ActivityName = activity.Name,
                            EarnedValue = runningTotal,
                            EarnedValuePercentage = percentage
                        });
                    }
                }

                m_EarnedValueChartPointSet.Clear();
                foreach (EarnedValuePoint point in pointSet)
                {
                    m_EarnedValueChartPointSet.Add(point);
                }
            }
        }

        private void SetEarnedValueChartPlotModel()
        {
            lock (m_Lock)
            {
                IList<EarnedValuePoint> pointSet = m_EarnedValueChartPointSet;
                PlotModel plotModel = null;
                if (pointSet != null
                    && pointSet.Any())
                {
                    plotModel = new PlotModel();
                    plotModel.Axes.Add(BuildEarnedValueChartXAxis());
                    plotModel.Axes.Add(BuildEarnedValueChartYAxis());
                    plotModel.LegendPlacement = LegendPlacement.Outside;
                    plotModel.LegendPosition = LegendPosition.RightMiddle;

                    var lineSeries = new LineSeries();
                    foreach (EarnedValuePoint point in pointSet)
                    {
                        lineSeries.Points.Add(new DataPoint(CalculateChartTimeXValue(point.Time), point.EarnedValuePercentage));
                    }
                    plotModel.Series.Add(lineSeries);
                }
                EarnedValueChartPlotModel = plotModel;
            }
        }

        private Axis BuildEarnedValueChartXAxis()
        {
            lock (m_Lock)
            {
                IList<IDependentActivity<int>> dependentActivities = GraphCompilation?.DependentActivities;
                Axis axis = null;
                if (dependentActivities != null
                    && dependentActivities.Any())
                {
                    int finishTime = dependentActivities.Max(x => x.EarliestFinishTime.GetValueOrDefault());
                    double minValue = CalculateChartTimeXValue(0);
                    double maxValue = CalculateChartTimeXValue(finishTime);
                    if (ShowDates)
                    {
                        axis = new DateTimeAxis
                        {
                            Position = AxisPosition.Bottom,
                            Minimum = minValue,
                            Maximum = maxValue,
                            Title = Properties.Resources.Label_TimeAxisTitle,
                            StringFormat = "d"
                        };
                    }
                    else
                    {
                        axis = new LinearAxis
                        {
                            Position = AxisPosition.Bottom,
                            Minimum = minValue,
                            Maximum = maxValue,
                            Title = Properties.Resources.Label_TimeAxisTitle
                        };
                    }
                }
                else
                {
                    axis = new LinearAxis();
                }
                return axis;
            }
        }

        private static Axis BuildEarnedValueChartYAxis()
        {
            return new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 0.0,
                Maximum = 100.0,
                Title = Properties.Resources.Label_EarnedValuePercentageAxisTitle
            };
        }

        private Task<DataTable> BuildEarnedValueChartDataTableAsync()
        {
            return Task.Run(() => BuildEarnedValueChartDataTable());
        }

        private DataTable BuildEarnedValueChartDataTable()
        {
            lock (m_Lock)
            {
                var table = new DataTable();
                IList<EarnedValuePoint> pointSet = m_EarnedValueChartPointSet;
                if (pointSet != null
                    && pointSet.Any())
                {
                    table.Columns.Add(new DataColumn(Properties.Resources.Label_TimeAxisTitle));
                    table.Columns.Add(new DataColumn(Properties.Resources.Label_Id));
                    table.Columns.Add(new DataColumn(Properties.Resources.Label_ActivityName));
                    table.Columns.Add(new DataColumn(Properties.Resources.Label_EarnedValueTitle));
                    table.Columns.Add(new DataColumn(Properties.Resources.Label_EarnedValuePercentageAxisTitle));

                    foreach (EarnedValuePoint point in pointSet)
                    {
                        var rowData = new List<object>
                        {
                            FormatScheduleOutput(point.Time),
                            point.ActivityId,
                            point.ActivityName,
                            point.EarnedValue,
                            point.EarnedValuePercentage
                        };
                        table.Rows.Add(rowData.ToArray());
                    }
                }
                return table;
            }
        }

        private double CalculateChartTimeXValue(int input)
        {
            lock (m_Lock)
            {
                double output = input;
                if (ShowDates)
                {
                    output = DateTimeAxis.ToDouble(m_DateTimeCalculator.AddDays(ProjectStart, input));
                }
                return output;
            }
        }

        private static Task DoExportDataTableToCsvAsync(DataTable dataTable, string filename)
        {
            if (dataTable == null)
            {
                throw new ArgumentException(nameof(dataTable));
            }
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException(nameof(filename));
            }
            return Task.Run(() =>
            {
                TextWriter writer = File.CreateText(filename); // This gets disposed by the CsvWriter.
                using (var csv = new CsvWriter(writer))
                {
                    foreach (DataColumn column in dataTable.Columns)
                    {
                        csv.WriteField(column.ColumnName);
                    }
                    csv.NextRecord();
                    foreach (DataRow row in dataTable.Rows)
                    {
                        for (var i = 0; i < dataTable.Columns.Count; i++)
                        {
                            csv.WriteField(row[i]);
                        }
                        csv.NextRecord();
                    }
                }
            });
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
                        ClearGraphMetricProperties();
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

        public async Task DoOpenArrowGraphSettingsAsync()
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
                await CalculateMetricsAsync();
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

        public async Task DoExportResourceChartToCsvAsync()
        {
            try
            {
                IsBusy = true;
                string directory = m_AppSettingService.ProjectPlanFolder;
                if (m_FileDialogService.ShowSaveDialog(
                    directory,
                    Properties.Resources.Filter_SaveCsvFileType,
                    Properties.Resources.Filter_SaveCsvFileExtension) == DialogResult.OK)
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
                        DataTable dataTable = await BuildResourceChartDataTableAsync();
                        await DoExportDataTableToCsvAsync(dataTable, filename);
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

        public async Task DoExportEarnedValueChartToCsvAsync()
        {
            try
            {
                IsBusy = true;
                string directory = m_AppSettingService.ProjectPlanFolder;
                if (m_FileDialogService.ShowSaveDialog(
                    directory,
                    Properties.Resources.Filter_SaveCsvFileType,
                    Properties.Resources.Filter_SaveCsvFileExtension) == DialogResult.OK)
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
                        DataTable dataTable = await BuildEarnedValueChartDataTableAsync();
                        await DoExportDataTableToCsvAsync(dataTable, filename);
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
                return m_ShowDates;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ShowDates = value;
                }
                RaisePropertyChanged(nameof(ShowDates));
                RaisePropertyChanged(nameof(ShowDays));
                SetCompilationOutput();
                SetResourceChartPlotModel();
                SetEarnedValueChartPlotModel();
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

        public double? DirectCost
        {
            get
            {
                return m_DirectCost;
            }
            private set
            {
                m_DirectCost = value;
                RaisePropertyChanged(nameof(DirectCost));
            }
        }

        public double? IndirectCost
        {
            get
            {
                return m_IndirectCost;
            }
            private set
            {
                m_IndirectCost = value;
                RaisePropertyChanged(nameof(IndirectCost));
            }
        }

        public double? OtherCost
        {
            get
            {
                return m_OtherCost;
            }
            private set
            {
                m_OtherCost = value;
                RaisePropertyChanged(nameof(OtherCost));
            }
        }

        public double? TotalCost
        {
            get
            {
                return m_TotalCost;
            }
            private set
            {
                m_TotalCost = value;
                RaisePropertyChanged(nameof(TotalCost));
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

                HasCompilationErrors = false;
                SetCompilationOutput();

                MetricsDto = null;
                ClearMetricProperties();
                ClearGraphMetricProperties();

                ArrowGraphDto = null;
                ArrowGraphData = GenerateArrowGraphData(ArrowGraphDto);
                HasStaleArrowGraph = false;

                m_ResourceChartSeriesSet.Clear();
                ResourceChartPlotModel = null;
                m_EarnedValueChartPointSet.Clear();
                EarnedValueChartPlotModel = null;
                ClearCostProperties();

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
            get;
            private set;
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

        #region IMetricsManagerViewModel Members

        public double? CriticalityRisk
        {
            get
            {
                return m_CriticalityRisk;
            }
            private set
            {
                m_CriticalityRisk = value;
                RaisePropertyChanged(nameof(CriticalityRisk));
            }
        }

        public double? FibonacciRisk
        {
            get
            {
                return m_FibonacciRisk;
            }
            private set
            {
                m_FibonacciRisk = value;
                RaisePropertyChanged(nameof(FibonacciRisk));
            }
        }

        public double? ActivityRisk
        {
            get
            {
                return m_ActivityRisk;
            }
            private set
            {
                m_ActivityRisk = value;
                RaisePropertyChanged(nameof(ActivityRisk));
            }
        }

        public double? ActivityRiskWithStdDevCorrection
        {
            get
            {
                return m_ActivityRiskWithStdDevCorrection;
            }
            private set
            {
                m_ActivityRiskWithStdDevCorrection = value;
                RaisePropertyChanged(nameof(ActivityRiskWithStdDevCorrection));
            }
        }

        public double? GeometricCriticalityRisk
        {
            get
            {
                return m_GeometricCriticalityRisk;
            }
            private set
            {
                m_GeometricCriticalityRisk = value;
                RaisePropertyChanged(nameof(GeometricCriticalityRisk));
            }
        }

        public double? GeometricFibonacciRisk
        {
            get
            {
                return m_GeometricFibonacciRisk;
            }
            private set
            {
                m_GeometricFibonacciRisk = value;
                RaisePropertyChanged(nameof(GeometricFibonacciRisk));
            }
        }

        public double? GeometricActivityRisk
        {
            get
            {
                return m_GeometricActivityRisk;
            }
            private set
            {
                m_GeometricActivityRisk = value;
                RaisePropertyChanged(nameof(GeometricActivityRisk));
            }
        }

        public int? CyclomaticComplexity
        {
            get
            {
                return m_CyclomaticComplexity;
            }
            private set
            {
                m_CyclomaticComplexity = value;
                RaisePropertyChanged(nameof(CyclomaticComplexity));
            }
        }

        public double? DurationManMonths
        {
            get
            {
                return m_DurationManMonths;
            }
            private set
            {
                m_DurationManMonths = value;
                RaisePropertyChanged(nameof(DurationManMonths));
            }
        }

        #endregion

        #region IResourceChartsManagerViewModel Members

        public bool ExportResourceChartAsCosts
        {
            get
            {
                return m_ExportResourceChartAsCosts;
            }
            set
            {
                m_ExportResourceChartAsCosts = value;
                RaisePropertyChanged(nameof(ExportResourceChartAsCosts));
            }
        }

        public PlotModel ResourceChartPlotModel
        {
            get
            {
                return m_ResourceChartPlotModel;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_ResourceChartPlotModel = value;
                }
                RaisePropertyChanged(nameof(ResourceChartPlotModel));
            }
        }

        public int ResourceChartOutputWidth
        {
            get
            {
                return m_ResourceChartOutputWidth;
            }
            set
            {
                m_ResourceChartOutputWidth = value;
                RaisePropertyChanged(nameof(ResourceChartOutputWidth));
            }
        }

        public int ResourceChartOutputHeight
        {
            get
            {
                return m_ResourceChartOutputHeight;
            }
            set
            {
                m_ResourceChartOutputHeight = value;
                RaisePropertyChanged(nameof(ResourceChartOutputHeight));
            }
        }

        public ICommand CopyResourceChartToClipboardCommand
        {
            get;
            private set;
        }

        public ICommand ExportResourceChartToCsvCommand
        {
            get;
            private set;
        }

        #endregion

        #region IEarnedValueChartsManagerViewModel Members

        public PlotModel EarnedValueChartPlotModel
        {
            get
            {
                return m_EarnedValueChartPlotModel;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_EarnedValueChartPlotModel = value;
                }
                RaisePropertyChanged(nameof(EarnedValueChartPlotModel));
            }
        }

        public int EarnedValueChartOutputWidth
        {
            get
            {
                return m_EarnedValueChartOutputWidth;
            }
            set
            {
                m_EarnedValueChartOutputWidth = value;
                RaisePropertyChanged(nameof(EarnedValueChartOutputWidth));
            }
        }

        public int EarnedValueChartOutputHeight
        {
            get
            {
                return m_EarnedValueChartOutputHeight;
            }
            set
            {
                m_EarnedValueChartOutputHeight = value;
                RaisePropertyChanged(nameof(EarnedValueChartOutputHeight));
            }
        }

        public ICommand CopyEarnedValueChartToClipboardCommand
        {
            get;
            private set;
        }

        public ICommand ExportEarnedValueChartToCsvCommand
        {
            get;
            private set;
        }

        #endregion

        #region Private Types

        private class ResourceSeries
        {
            public string Title { get; set; }
            public InterActivityAllocationType InterActivityAllocationType { get; set; }
            public IList<int> Values { get; set; }
            public ColorFormatDto ColorFormatDto { get; set; }
            public double UnitCost { get; set; }
            public int DisplayOrder { get; set; }
        }

        public class EarnedValuePoint
        {
            public int Time { get; set; }
            public string ActivityId { get; set; }
            public string ActivityName { get; set; }
            public int EarnedValue { get; set; }
            public double EarnedValuePercentage { get; set; }
        }

        #endregion
    }
}
