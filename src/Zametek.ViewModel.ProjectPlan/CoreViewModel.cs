using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class CoreViewModel
        : ViewModelBase, ICoreViewModel
    {
        #region Fields

        private readonly Lock m_Lock;
        private bool m_TrackIsProjectScenarioUpdated;
        private bool m_TrackHasStaleOutputs;

        private readonly VertexGraphCompiler m_VertexGraphCompiler;

        private readonly IProjectScenarioFileImport m_ProjectScenarioFileImport;
        private readonly IProjectScenarioFileExport m_ProjectScenarioFileExport;
        private readonly ISettingService m_SettingService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly ProjectPlanMapper m_Mapper;
        private readonly IGraphCompilationService m_GraphCompilationService;
        private readonly IResourceSchedulingService m_ResourceSchedulingService;
        private readonly IMetricCalculationService m_MetricCalculationService;

        private readonly IDisposable? m_ReadOnlyActivitiesSub;
        private readonly IDisposable? m_OrderableActivitiesSub;
        private readonly IDisposable? m_NetworkMetricsSub;
        private readonly IDisposable? m_AreActivitiesUncompiledSub;
        private readonly IDisposable? m_CompileOnSettingsUpdateSub;
        private readonly IDisposable? m_BuildArrowGraphSub;
        private readonly IDisposable? m_BuildVertexGraphSub;
        private readonly IDisposable? m_BuildResourceSeriesSetSub;
        private readonly IDisposable? m_BuildTrackingSeriesSetSub;
        private readonly IDisposable? m_BuildRiskMetricsSub;
        private readonly IDisposable? m_BuildFinancialMetricsSub;

        #endregion

        #region Ctors

        public CoreViewModel(
            IProjectScenarioFileImport projectScenarioFileImport,
            IProjectScenarioFileExport projectScenarioFileExport,
            ISettingService settingService,
            IDateTimeCalculator dateTimeCalculator,
            ProjectPlanMapper mapper,
            IGraphCompilationService graphCompilationService,
            IResourceSchedulingService resourceSchedulingService,
            IMetricCalculationService metricCalculationService)
        {
            ArgumentNullException.ThrowIfNull(projectScenarioFileImport);
            ArgumentNullException.ThrowIfNull(projectScenarioFileExport);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(graphCompilationService);
            ArgumentNullException.ThrowIfNull(resourceSchedulingService);
            ArgumentNullException.ThrowIfNull(metricCalculationService);
            m_Lock = new();
            m_TrackIsProjectScenarioUpdated = true;
            m_TrackHasStaleOutputs = true;
            m_VertexGraphCompiler = new VertexGraphCompiler();
            m_ProjectScenarioFileImport = projectScenarioFileImport;
            m_ProjectScenarioFileExport = projectScenarioFileExport;
            m_SettingService = settingService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_DisplaySettingsViewModel = new ProjectScenarioDisplaySettingsViewModel(
                m_DateTimeCalculator,
                SetIsProjectScenarioUpdated,
                () => IsReadyToCompile = ReadyToCompile.Yes);
            m_Mapper = mapper;
            m_GraphCompilationService = graphCompilationService;
            m_ResourceSchedulingService = resourceSchedulingService;
            m_MetricCalculationService = metricCalculationService;

            m_IsReadyToCompile = ReadyToCompile.No;
            m_IsBusy = false;
            m_HasStaleOutputs = false;
            m_ProjectStart = new(DateTime.Today);
            m_Today = new(DateTime.Today);
            m_ResourceSettings = new ResourceSettingsModel();
            m_Activities = new();
            m_GraphSettings = m_SettingService.DefaultGraphSettings;
            m_ResourceSettings = m_SettingService.DefaultResourceSettings;
            m_WorkStreamSettings = m_SettingService.DefaultWorkStreamSettings;
            m_HolidaySettings = m_SettingService.DefaultHolidaySettings;
            m_RiskMetrics = new();
            m_CostMetrics = new();
            m_BillingMetrics = new();
            m_MarginMetrics = new();
            m_EffortMetrics = new();
            m_NetworkMetrics = new();

            DisplaySettingsViewModel.ShowDates = m_SettingService.DefaultShowDates;
            DisplaySettingsViewModel.UseClassicDates = m_SettingService.DefaultUseClassicDates;
            DisplaySettingsViewModel.NonWorkingDayMode = m_SettingService.DefaultNonWorkingDayMode;
            DisplaySettingsViewModel.HideCost = m_SettingService.DefaultHideCost;
            DisplaySettingsViewModel.HideBilling = m_SettingService.DefaultHideBilling;

            m_GraphCompilation = new GraphCompilation<int, int, int, DependentActivity>([], [], []);
            m_ArrowGraph = new ArrowGraphModel();
            m_VertexGraph = new VertexGraphModel();
            m_ResourceSeriesSet = new ResourceSeriesSetModel();
            m_TrackingSeriesSet = new TrackingSeriesSetModel();

            m_OrderableActivities = [];

            m_SelectedTheme = m_SettingService.SelectedTheme;

            m_ProjectFinish = this
                .WhenAnyValue(
                    core => core.DisplaySettingsViewModel.ShowDates,
                    core => core.ProjectStart,
                    core => core.NetworkMetrics,
                    core => core.HolidaySettings,
                    core => core.m_DateTimeCalculator.NonWorkingDayMode,
                    core => core.m_DateTimeCalculator.DisplayMode,
                    (bool showDates, DateTimeOffset projectStart, NetworkModel networkModel, HolidaySettingsModel _, NonWorkingDayMode _, DateTimeDisplayMode _) =>
                    {
                        if (networkModel.Duration is null || networkModel.Duration == 0)
                        {
                            return string.Empty;
                        }

                        if (showDates)
                        {
                            int durationValue = networkModel.Duration.GetValueOrDefault();
                            DateTimeOffset startAndFinish = m_DateTimeCalculator.AddDays(projectStart, durationValue);
                            return m_DateTimeCalculator
                                .DisplayFinishDate(startAndFinish, startAndFinish, 1)
                                .ToString(DateTimeCalculator.DateFormat);
                        }

                        return networkModel.Duration.GetValueOrDefault().ToString();
                    })
                .ToProperty(this, mm => mm.ProjectFinish);

            // Create read-only view to the source list.
            m_ReadOnlyActivitiesSub = m_Activities.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out m_ReadOnlyActivities)
               .Subscribe();

            m_OrderableActivitiesSub = m_Activities.Connect()
               .ObserveOn(RxApp.MainThreadScheduler) // Ensure UI thread safety
               .Bind(m_OrderableActivities)          // Bind to the mutable collection
               .DisposeMany()                        // Clean up resources
               .Subscribe();

            m_HasActivities = m_ReadOnlyActivities
                .ToObservableChangeSet()
                .Select(x => m_ReadOnlyActivities.Count > 0)
                .ToProperty(this, core => core.HasActivities);

            m_HasResources = this
                .WhenAnyValue(
                    core => core.ResourceSettings,
                    settings => settings.Resources.Count > 0 && !settings.AreDisabled)
                .ToProperty(this, core => core.HasResources);

            m_HasWorkStreams = this
                .WhenAnyValue(
                    core => core.WorkStreamSettings,
                    settings => settings.WorkStreams.Count > 0)
                .ToProperty(this, core => core.HasWorkStreams);

            m_HasPhases = this
                .WhenAnyValue(
                    core => core.WorkStreamSettings,
                    settings => settings.WorkStreams.Any(x => x.IsPhase))
                .ToProperty(this, core => core.HasPhases);

            m_AreActivitiesUncompiledSub = m_ReadOnlyActivities
                .ToObservableChangeSet()
                .AutoRefresh(activity => activity.IsCompiled) // Subscribe only to IsCompiled property changes
                .Filter(activity => !activity.IsCompiled)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(changeSet =>
                {
                    if ((changeSet.Replaced + changeSet.Adds) > 0)
                    {
                        lock (m_Lock)
                        {
                            if (!IsBusy)
                            {
                                if (AutoCompile)
                                {
                                    IsReadyToReviseTrackers = ReadyToRevise.Yes;
                                    IsReadyToCompile = ReadyToCompile.Yes;
                                }
                                else
                                {
                                    IsReadyToReviseTrackers = ReadyToRevise.No;
                                    IsReadyToCompile = ReadyToCompile.No;
                                }
                            }
                        }
                    }
                });

            m_CompileOnSettingsUpdateSub = this
                .WhenAnyValue(core => core.IsReadyToCompile)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(isReady =>
                {
                    lock (m_Lock)
                    {
                        if (isReady == ReadyToCompile.Yes
                            && !IsBusy)
                        {
                            RunAutoCompile();
                        }
                    }
                });

            m_BuildArrowGraphSub = this
                .WhenAnyValue(core => core.GraphCompilation)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildArrowGraph());

            m_BuildVertexGraphSub = this
                .WhenAnyValue(core => core.GraphCompilation)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildVertexGraph());

            m_BuildResourceSeriesSetSub = this
                .WhenAnyValue(core => core.GraphCompilation)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildResourceSeriesSet());

            m_BuildTrackingSeriesSetSub = this
                .WhenAnyValue(core => core.GraphCompilation)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildTrackingSeriesSet());

            m_NetworkMetricsSub = this
                .WhenAnyValue(
                    core => core.GraphCompilation,
                    core => core.HasCompilationErrors)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildNetworkMetrics());

            m_BuildRiskMetricsSub = this
                .WhenAnyValue(
                    core => core.GraphCompilation,
                    core => core.GraphSettings,
                    core => core.HasCompilationErrors)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildRiskMetrics());

            m_BuildFinancialMetricsSub = this
                .WhenAnyValue(
                    core => core.ResourceSeriesSet,
                    core => core.HasCompilationErrors)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildFinancialMetrics());
        }

        #endregion

        #region Private Methods

        private void SetIsProjectScenarioUpdated(bool isProjectScenarioUpdated, bool trackStaleOutputs)
        {
            lock (m_Lock)
            {
                bool originalTrackValue = m_TrackHasStaleOutputs;
                m_TrackHasStaleOutputs = originalTrackValue && trackStaleOutputs;
                IsProjectScenarioUpdated = isProjectScenarioUpdated;
                m_TrackHasStaleOutputs = originalTrackValue;
            }
        }

        #endregion

        #region ICoreViewModel Members

        private bool m_IsBusy;
        public bool IsBusy
        {
            get => m_IsBusy;
            private set
            {
                this.RaiseAndSetIfChanged(ref m_IsBusy, value);
            }
        }

        // We need to use an enum because raised changes on bools aren't always captured.
        // https://github.com/reactiveui/ReactiveUI/issues/3846
        private ReadyToCompile m_IsReadyToCompile;

        // This should always be the last thing altered in order to trigger a compile.
        public ReadyToCompile IsReadyToCompile
        {
            get => m_IsReadyToCompile;
            private set
            {
                lock (m_Lock)
                {
                    if (m_TrackIsProjectScenarioUpdated)
                    {
                        this.RaiseAndSetIfChanged(ref m_IsReadyToCompile, value);
                    }
                }
            }
        }

        private bool m_IsProjectScenarioUpdated;
        public bool IsProjectScenarioUpdated
        {
            get => m_IsProjectScenarioUpdated;
            set
            {
                lock (m_Lock)
                {
                    HasStaleOutputs = value;
                    if (m_TrackIsProjectScenarioUpdated)
                    {
                        this.RaiseAndSetIfChanged(ref m_IsProjectScenarioUpdated, value);
                    }
                }
            }
        }

        private bool m_HasStaleOutputs;
        public bool HasStaleOutputs
        {
            get => m_HasStaleOutputs;
            set
            {
                lock (m_Lock)
                {
                    if (m_TrackHasStaleOutputs)
                    {
                        this.RaiseAndSetIfChanged(ref m_HasStaleOutputs, value);
                    }
                }
            }
        }

        private DateTimeOffset m_ProjectStart;
        public DateTimeOffset ProjectStart
        {
            get => m_ProjectStart;
            set
            {
                lock (m_Lock)
                {
                    IsProjectScenarioUpdated = true;

                    // Convert to local now using TimeProvider as we do not know
                    // if the input is provided as just a datetime from XAML.
                    m_DateTimeCalculator.ProjectStart = value;
                    this.RaiseAndSetIfChanged(ref m_ProjectStart, m_DateTimeCalculator.ProjectStart);
                    IsReadyToCompile = ReadyToCompile.Yes;
                }
            }
        }

        private DateTimeOffset m_Today;
        public DateTimeOffset Today
        {
            get => m_Today;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);

                    // Convert to local now using TimeProvider as we do not know
                    // if the input is provided as just a datetime from XAML.
                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocal(value.DateTime);

                    this.RaiseAndSetIfChanged(ref m_Today, localNow);
                }
            }
        }

        private readonly ObservableAsPropertyHelper<string> m_ProjectFinish;
        public string ProjectFinish => m_ProjectFinish.Value;

        private readonly ProjectScenarioDisplaySettingsViewModel m_DisplaySettingsViewModel;
        public IProjectScenarioDisplaySettingsViewModel DisplaySettingsViewModel
        {
            get => m_DisplaySettingsViewModel;
        }

        public bool DefaultShowDates
        {
            get => m_SettingService.DefaultShowDates;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.DefaultShowDates = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool DefaultUseClassicDates
        {
            get => m_SettingService.DefaultUseClassicDates;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.DefaultUseClassicDates = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public NonWorkingDayMode DefaultNonWorkingDayMode
        {
            get => m_SettingService.DefaultNonWorkingDayMode;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.DefaultNonWorkingDayMode = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool DefaultHideCost
        {
            get => m_SettingService.DefaultHideCost;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.DefaultHideCost = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool DefaultHideBilling
        {
            get => m_SettingService.DefaultHideBilling;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.DefaultHideBilling = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private bool m_AutoCompile;
        public bool AutoCompile
        {
            get => m_AutoCompile;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_AutoCompile, value);

                    if (m_AutoCompile)
                    {
                        IsReadyToCompile = ReadyToCompile.No;
                    }
                }
            }
        }

        private string m_SelectedTheme;

        public string SelectedTheme
        {
            get => m_SelectedTheme;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.SelectedTheme = value;
                    this.RaiseAndSetIfChanged(ref m_SelectedTheme, value);
                }
            }
        }

        private BaseTheme m_BaseTheme;
        public BaseTheme BaseTheme
        {
            get => m_BaseTheme;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_BaseTheme, value);
                }
            }
        }

        private readonly SourceList<IManagedActivityViewModel> m_Activities;
        public IReadOnlyList<IManagedActivityViewModel> RawActivities => m_Activities.Items;

        private readonly ReadOnlyObservableCollection<IManagedActivityViewModel> m_ReadOnlyActivities;
        public ReadOnlyObservableCollection<IManagedActivityViewModel> Activities => m_ReadOnlyActivities;

        private readonly ObservableCollectionExtended<IManagedActivityViewModel> m_OrderableActivities;
        public ObservableCollection<IManagedActivityViewModel> OrderableActivities => m_OrderableActivities;

        private GraphSettingsModel m_GraphSettings;
        public GraphSettingsModel GraphSettings
        {
            get => m_GraphSettings;
            set
            {
                lock (m_Lock)
                {
                    m_GraphSettings = value;
                    IsProjectScenarioUpdated = true;
                    this.RaisePropertyChanged();
                    IsReadyToCompile = ReadyToCompile.Yes;
                }
            }
        }

        private ResourceSettingsModel m_ResourceSettings;
        public ResourceSettingsModel ResourceSettings
        {
            get => m_ResourceSettings;
            set
            {
                lock (m_Lock)
                {
                    m_ResourceSettings = value;
                    IsProjectScenarioUpdated = true;
                    this.RaisePropertyChanged();
                    IsReadyToCompile = ReadyToCompile.Yes;
                }
            }
        }

        private WorkStreamSettingsModel m_WorkStreamSettings;
        public WorkStreamSettingsModel WorkStreamSettings
        {
            get => m_WorkStreamSettings;
            set
            {
                lock (m_Lock)
                {
                    m_WorkStreamSettings = value;
                    IsProjectScenarioUpdated = true;
                    this.RaisePropertyChanged();
                    IsReadyToCompile = ReadyToCompile.Yes;
                }
            }
        }

        private HolidaySettingsModel m_HolidaySettings;
        public HolidaySettingsModel HolidaySettings
        {
            get => m_HolidaySettings;
            set
            {
                lock (m_Lock)
                {
                    //// Filter out any recurrence patterns that
                    //// represent every day.
                    //HolidaySettingsModel holidaySettingsModel = value;
                    //List<HolidayModel> validHolidays = [.. holidaySettingsModel.Holidays
                    //    .Where(x =>
                    //        !RecurrenceRuleHelper.IsRecurrenceRuleEveryDay(
                    //            RecurrencePatternHelper.ToRule(x.RecurrencePattern)))];

                    //holidaySettingsModel.Holidays.Clear();
                    //holidaySettingsModel.Holidays.AddRange(validHolidays);

                    m_HolidaySettings = value;
                    m_DateTimeCalculator.SetNonWorkingDayCalendarEvents(m_HolidaySettings.Holidays);
                    IsProjectScenarioUpdated = true;
                    this.RaisePropertyChanged();
                    IsReadyToCompile = ReadyToCompile.Yes;
                }
            }
        }

        public MetricsModel Metrics
        {
            get
            {
                return new MetricsModel
                {
                    Risks = RiskMetrics,
                    Costs = CostMetrics,
                    Billings = BillingMetrics,
                    Margins = MarginMetrics,
                    Efforts = EffortMetrics,
                    Network = NetworkMetrics,
                };
            }
            private set
            {
                // These are broken down individually so that change notifications only
                // happen if the original Metrics record from the compiled version is
                // different to the loaded file version.
                RiskMetrics = value.Risks;
                CostMetrics = value.Costs;
                BillingMetrics = value.Billings;
                MarginMetrics = value.Margins;
                EffortMetrics = value.Efforts;
                NetworkMetrics = value.Network;
                this.RaisePropertyChanged();
            }
        }

        private RisksModel m_RiskMetrics;
        public RisksModel RiskMetrics
        {
            get => m_RiskMetrics;
            private set
            {
                lock (m_Lock)
                {
                    if (m_RiskMetrics == value)
                    {
                        return;
                    }
                    m_RiskMetrics = value;
                    SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(Metrics));
                }
            }
        }

        private CostsModel m_CostMetrics;
        public CostsModel CostMetrics
        {
            get => m_CostMetrics;
            private set
            {
                lock (m_Lock)
                {
                    if (m_CostMetrics == value)
                    {
                        return;
                    }
                    m_CostMetrics = value;
                    SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(Metrics));
                }
            }
        }

        private BillingsModel m_BillingMetrics;
        public BillingsModel BillingMetrics
        {
            get => m_BillingMetrics;
            private set
            {
                lock (m_Lock)
                {
                    if (m_BillingMetrics == value)
                    {
                        return;
                    }
                    m_BillingMetrics = value;
                    SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(Metrics));
                }
            }
        }

        private MarginsModel m_MarginMetrics;
        public MarginsModel MarginMetrics
        {
            get => m_MarginMetrics;
            private set
            {
                lock (m_Lock)
                {
                    if (m_MarginMetrics == value)
                    {
                        return;
                    }
                    m_MarginMetrics = value;
                    SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(Metrics));
                }
            }
        }

        private EffortsModel m_EffortMetrics;
        public EffortsModel EffortMetrics
        {
            get => m_EffortMetrics;
            private set
            {
                lock (m_Lock)
                {
                    if (m_EffortMetrics == value)
                    {
                        return;
                    }
                    m_EffortMetrics = value;
                    SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(Metrics));
                }
            }
        }

        private NetworkModel m_NetworkMetrics;
        public NetworkModel NetworkMetrics
        {
            get => m_NetworkMetrics;
            private set
            {
                lock (m_Lock)
                {
                    if (m_NetworkMetrics == value)
                    {
                        return;
                    }
                    m_NetworkMetrics = value;
                    SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(Metrics));
                }
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_HasActivities;
        public bool HasActivities => m_HasActivities.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasResources;
        public bool HasResources => m_HasResources.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasWorkStreams;
        public bool HasWorkStreams => m_HasWorkStreams.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasPhases;
        public bool HasPhases => m_HasPhases.Value;

        private bool m_HasCompilationErrors;
        public bool HasCompilationErrors
        {
            get => m_HasCompilationErrors;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_HasCompilationErrors, value);
                }
            }
        }

        private IGraphCompilation<int, int, int, IDependentActivity> m_GraphCompilation;
        public IGraphCompilation<int, int, int, IDependentActivity> GraphCompilation
        {
            get => m_GraphCompilation;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_GraphCompilation, value);
                }
            }
        }

        private ArrowGraphModel m_ArrowGraph;
        public ArrowGraphModel ArrowGraph
        {
            get => m_ArrowGraph;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ArrowGraph, value);
                }
            }
        }

        private VertexGraphModel m_VertexGraph;
        public VertexGraphModel VertexGraph
        {
            get => m_VertexGraph;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_VertexGraph, value);
                }
            }
        }

        private ResourceSeriesSetModel m_ResourceSeriesSet;
        public ResourceSeriesSetModel ResourceSeriesSet
        {
            get => m_ResourceSeriesSet;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ResourceSeriesSet, value);
                }
            }
        }

        private TrackingSeriesSetModel m_TrackingSeriesSet;
        public TrackingSeriesSetModel TrackingSeriesSet
        {
            get => m_TrackingSeriesSet;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_TrackingSeriesSet, value);
                }
            }
        }

        private int m_TrackerIndex;
        public int TrackerIndex
        {
            get => m_TrackerIndex;
            set => this.RaiseAndSetIfChanged(ref m_TrackerIndex, value);
        }

        private ReadyToRevise m_IsReadyToReviseTrackers;
        public ReadyToRevise IsReadyToReviseTrackers
        {
            get => m_IsReadyToReviseTrackers;
            set
            {
                m_IsReadyToReviseTrackers = value;
                this.RaisePropertyChanged();
            }
        }

        public int GetNextActivityId()
        {
            lock (m_Lock)
            {
                return m_VertexGraphCompiler.GetNextActivityId();
            }
        }

        public ProjectScenarioModel CreateEmptyProjectScenario()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    var plan = new ProjectScenarioModel
                    {
                        ProjectStart = new(DateTime.Today),
                        Today = new(DateTime.Today),
                        DependentActivities = [],
                        GraphSettings = m_SettingService.DefaultGraphSettings,
                        ResourceSettings = m_SettingService.DefaultResourceSettings,
                        WorkStreamSettings = m_SettingService.DefaultWorkStreamSettings,
                        HolidaySettings = m_SettingService.DefaultHolidaySettings,
                        Metrics = new(),
                        DisplaySettings = new ProjectScenarioDisplaySettingsModel
                        {
                            ShowDates = m_SettingService.DefaultShowDates,
                            UseClassicDates = m_SettingService.DefaultUseClassicDates,
                            NonWorkingDayMode = m_SettingService.DefaultNonWorkingDayMode,
                            HideCost = m_SettingService.DefaultHideCost,
                            HideBilling = m_SettingService.DefaultHideBilling,
                        },
                    };

                    return plan;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ClearSettings()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ProjectScenarioModel emptyPlan = CreateEmptyProjectScenario();

                    ProjectStart = emptyPlan.ProjectStart;
                    Today = emptyPlan.Today;

                    GraphSettings = emptyPlan.GraphSettings;
                    ResourceSettings = emptyPlan.ResourceSettings;
                    WorkStreamSettings = emptyPlan.WorkStreamSettings;
                    HolidaySettings = emptyPlan.HolidaySettings;

                    ProjectScenarioDisplaySettingsModel defaultDisplaySettings = emptyPlan.DisplaySettings;
                    DisplaySettingsViewModel.SetValues(defaultDisplaySettings);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ResetProjectScenario()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    m_TrackIsProjectScenarioUpdated = false;
                    m_TrackHasStaleOutputs = false;

                    ClearManagedActivities();

                    ClearSettings();

                    m_SettingService.ResetProjectScenario();

                    Metrics = new();

                    HasCompilationErrors = false;
                    GraphCompilation = new GraphCompilation<int, int, int, DependentActivity>([], [], []);

                    ArrowGraph = new();
                    VertexGraph = new();

                    IsReadyToCompile = ReadyToCompile.No;
                    IsReadyToReviseTrackers = ReadyToRevise.No;

                    m_TrackIsProjectScenarioUpdated = true;
                    IsProjectScenarioUpdated = false;

                    m_TrackHasStaleOutputs = true;
                    HasStaleOutputs = false;
                }
            }
            finally
            {
                m_TrackIsProjectScenarioUpdated = true;
                m_TrackHasStaleOutputs = true;
                IsBusy = false;
            }
        }

        public ProjectScenarioImportModel ImportProjectScenarioFile(string filename)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    return m_ProjectScenarioFileImport.ImportProjectScenarioFile(filename);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ExportProjectScenarioFile(
            ProjectScenarioModel projectScenarioModel,
            ResourceSeriesSetModel resourceSeriesSetModel,
            TrackingSeriesSetModel trackingSeriesSetModel,
            bool showDates,
            string filename)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    m_ProjectScenarioFileExport.ExportProjectScenarioFile(
                        projectScenarioModel,
                        resourceSeriesSetModel,
                        trackingSeriesSetModel,
                        showDates,
                        filename);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ProcessProjectScenarioImport(
            ProjectScenarioImportModel projectScenarioImportModel,
            Guid projectScenarioId,
            string projectScenarioTitle)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ResetProjectScenario();
                    m_TrackIsProjectScenarioUpdated = false;
                    m_TrackHasStaleOutputs = false;
                    m_SettingService.SetProjectScenarioId(projectScenarioId);
                    m_SettingService.SetProjectScenarioTitle(projectScenarioTitle);

                    // Default display mode is required for all file opening and closing.
                    m_DateTimeCalculator.DisplayMode = DateTimeDisplayMode.Default;

                    // Project Start Date.
                    ProjectStart = projectScenarioImportModel.ProjectStart;

                    // Project Start Date.
                    Today = projectScenarioImportModel.Today;

                    // Holiday settings.

                    HolidaySettingsModel holidaySettings = m_SettingService.DefaultHolidaySettings.CloneObject();

                    if (projectScenarioImportModel.Holidays.Count != 0)
                    {
                        holidaySettings.Holidays.Clear();

                        foreach (HolidayModel holiday in projectScenarioImportModel.Holidays)
                        {
                            holidaySettings.Holidays.Add(holiday);
                        }
                    }

                    HolidaySettings = holidaySettings;

                    // Work Stream settings.
                    WorkStreamSettingsModel workStreamSettings = m_SettingService.DefaultWorkStreamSettings.CloneObject();

                    if (projectScenarioImportModel.WorkStreams.Count != 0)
                    {
                        workStreamSettings.WorkStreams.Clear();

                        foreach (WorkStreamModel workStream in projectScenarioImportModel.WorkStreams)
                        {
                            workStreamSettings.WorkStreams.Add(workStream);
                        }
                    }

                    WorkStreamSettings = workStreamSettings;

                    // Resources.
                    ResourceSettingsModel resourceSettings = m_SettingService.DefaultResourceSettings.CloneObject();
                    resourceSettings = resourceSettings with
                    {
                        DefaultUnitCost = projectScenarioImportModel.ResourceSettings.DefaultUnitCost,
                        DefaultUnitBilling = projectScenarioImportModel.ResourceSettings.DefaultUnitBilling,
                        AreDisabled = projectScenarioImportModel.ResourceSettings.AreDisabled,
                    };

                    if (projectScenarioImportModel.ResourceSettings.Resources.Count != 0)
                    {
                        resourceSettings.Resources.Clear();

                        foreach (ResourceModel resource in projectScenarioImportModel.ResourceSettings.Resources)
                        {
                            resourceSettings.Resources.Add(resource);
                        }
                    }

                    ResourceSettings = resourceSettings;

                    // Graph settings.
                    GraphSettingsModel graphSettings = m_SettingService.DefaultGraphSettings.CloneObject();

                    if (projectScenarioImportModel.ActivitySeverities.Count != 0)
                    {
                        graphSettings.ActivitySeverities.Clear();

                        foreach (ActivitySeverityModel activitySeverity in projectScenarioImportModel.ActivitySeverities)
                        {
                            graphSettings.ActivitySeverities.Add(activitySeverity);
                        }
                    }

                    GraphSettings = graphSettings;

                    // Activities.
                    // Be sure to set the ResourceSettings first, so that the activities know
                    // which resources are being referred to when marking them as selected.
                    AddManagedActivities(projectScenarioImportModel.DependentActivities);

                    // Display settings.
                    DisplaySettingsViewModel.SetValues(projectScenarioImportModel.DisplaySettings);

                    RunCompile();

                    //// Metrics.
                    //// It is important to put this after the compilation, so it will only
                    //// trigger a project scenario updated event if it is different from the compiled metrics.
                    //Metrics = projectScenarioModel.Metrics;

                    m_TrackIsProjectScenarioUpdated = true;
                    IsProjectScenarioUpdated = true;
                    m_TrackHasStaleOutputs = true;
                }
            }
            finally
            {
                m_TrackIsProjectScenarioUpdated = true;
                m_TrackHasStaleOutputs = true;
                IsBusy = false;
            }
        }

        public void ProcessProjectScenario(
            ProjectScenarioModel projectScenarioModel,
            Guid projectScenarioId,
            string projectScenarioTitle)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ResetProjectScenario();
                    m_TrackIsProjectScenarioUpdated = false;
                    m_TrackHasStaleOutputs = false;
                    m_SettingService.SetProjectScenarioId(projectScenarioId);
                    m_SettingService.SetProjectScenarioTitle(projectScenarioTitle);

                    // Default display mode is required for all file opening and closing.
                    m_DateTimeCalculator.DisplayMode = DateTimeDisplayMode.Default;

                    // Project Start Date.
                    ProjectStart = projectScenarioModel.ProjectStart;

                    // Project Start Date.
                    Today = projectScenarioModel.Today;

                    // Display settings.
                    var displaySettings = projectScenarioModel.DisplaySettings with
                    {
                        ShowDates = DisplaySettingsViewModel.ShowDates,
                        UseClassicDates = DisplaySettingsViewModel.UseClassicDates,
                        NonWorkingDayMode = DisplaySettingsViewModel.NonWorkingDayMode,
                    };

                    DisplaySettingsViewModel.SetValues(displaySettings);

                    // Holiday Settings.
                    HolidaySettings = projectScenarioModel.HolidaySettings;

                    // Work Stream Settings.
                    WorkStreamSettings = projectScenarioModel.WorkStreamSettings;

                    // Resource Settings.
                    ResourceSettings = projectScenarioModel.ResourceSettings;

                    // Graph Settings.
                    GraphSettings = projectScenarioModel.GraphSettings;

                    // Activities.
                    AddManagedActivities(projectScenarioModel.DependentActivities);

                    // Now that Resources and Activities are in place,
                    // revise all tracker values.
                    IsReadyToReviseTrackers = ReadyToRevise.Yes;

                    // Display settings (the rest of the settings).
                    displaySettings = projectScenarioModel.DisplaySettings with
                    {
                        ShowDates = projectScenarioModel.DisplaySettings.ShowDates,
                        UseClassicDates = projectScenarioModel.DisplaySettings.UseClassicDates,
                        NonWorkingDayMode = projectScenarioModel.DisplaySettings.NonWorkingDayMode,
                    };

                    DisplaySettingsViewModel.SetValues(displaySettings);

                    RunCompile();

                    // Metrics.
                    // It is important to put this after the compilation, so it will only
                    // trigger a project plan updated event if it is different from the compiled metrics.
                    Metrics = projectScenarioModel.Metrics;

                    m_TrackIsProjectScenarioUpdated = true;
                    IsProjectScenarioUpdated = false;
                    m_TrackHasStaleOutputs = true;
                }
            }
            finally
            {
                m_TrackIsProjectScenarioUpdated = true;
                m_TrackHasStaleOutputs = true;
                IsBusy = false;
            }
        }

        public ProjectScenarioModel BuildProjectScenario()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    var graphCompilation = m_Mapper.ToGraphCompilationModel(GraphCompilation);

                    // Default display mode is required for all file opening and closing.
                    DateTimeDisplayMode oldDisplayMode = m_DateTimeCalculator.DisplayMode;
                    m_DateTimeCalculator.DisplayMode = DateTimeDisplayMode.Default;

                    UpdateActivityDisplayOrders();

                    List<DependentActivityModel> dependentActivities =
                        [.. RawActivities.Cast<ManagedActivityViewModel>().Select(m_Mapper.ToDependentActivityModel)];

                    var plan = new ProjectScenarioModel
                    {
                        ProjectStart = ProjectStart,
                        Today = Today,
                        DependentActivities = dependentActivities,
                        GraphSettings = GraphSettings.CloneObject(),
                        ResourceSettings = ResourceSettings.CloneObject(),
                        WorkStreamSettings = WorkStreamSettings.CloneObject(),
                        HolidaySettings = HolidaySettings.CloneObject(),
                        Metrics = Metrics.CloneObject(),
                        DisplaySettings = DisplaySettingsViewModel.GetValues(),
                    };

                    // Reorder activity dependencies so they are more readable.
                    foreach (DependentActivityModel activityModel in plan.DependentActivities)
                    {
                        activityModel.Dependencies.Sort();
                        activityModel.PlanningDependencies.Sort();
                        activityModel.ResourceDependencies.Sort();
                        activityModel.Successors.Sort();
                    }

                    // Put display mode back to the way it was.
                    m_DateTimeCalculator.DisplayMode = oldDisplayMode;

                    return plan;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public int AddManagedActivity(int displayOrder)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    var activityId = GetNextActivityId();
                    var set = new HashSet<DependentActivityModel>
                    {
                        new()
                        {
                            Activity = new ActivityModel
                            {
                                Id = activityId,
                                DisplayOrder = displayOrder,
                            }
                        }
                    };
                    AddManagedActivities(set);
                    return activityId;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void AddManagedActivities(IEnumerable<DependentActivityModel> dependentActivityModels)
        {
            try
            {
                lock (m_Lock)
                {
                    m_Activities.Edit(activities =>
                    {
                        IsBusy = true;

                        IOrderedEnumerable<DependentActivityModel> orderedDependentActivityModels = dependentActivityModels
                            .OrderBy(x => x.Activity.DisplayOrder)
                            .ThenBy(x => x.Activity.Id);

                        foreach (DependentActivityModel dependentActivity in orderedDependentActivityModels)
                        {
                            var activity = new ManagedActivityViewModel(
                                this,
                                m_Mapper.ToDependentActivity(dependentActivity),
                                m_DateTimeCalculator,
                                m_VertexGraphCompiler,
                                ProjectStart,
                                dependentActivity.Activity.Trackers,
                                dependentActivity.Activity.MinimumEarliestStartDateTime,
                                dependentActivity.Activity.MaximumLatestFinishDateTime);

                            if (m_VertexGraphCompiler.AddActivity(activity))
                            {
                                activities.Add(activity);
                            }
                            else
                            {
                                activity.Dispose();
                            }
                        }
                    });

                    UpdateActivityDisplayOrders();
                    //IsProjectScenarioUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void RemoveManagedActivities(IEnumerable<int> dependentActivityIds)
        {
            try
            {
                lock (m_Lock)
                {
                    m_Activities.Edit(activities =>
                    {
                        IsBusy = true;
                        IEnumerable<IManagedActivityViewModel> dependentActivities = [.. RawActivities.Where(x => dependentActivityIds.Contains(x.Id))];

                        foreach (IManagedActivityViewModel dependentActivity in dependentActivities)
                        {
                            if (m_VertexGraphCompiler.RemoveActivity(dependentActivity.Id))
                            {
                                activities.Remove(dependentActivity);
                                dependentActivity.Dispose();
                            }
                        }
                    });

                    UpdateActivityDisplayOrders();
                    IsProjectScenarioUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void UpdateManagedActivities(IEnumerable<UpdateDependentActivityModel> updateModels)
        {
            try
            {
                lock (m_Lock)
                {
                    m_Activities.Edit(activities =>
                    {
                        IsBusy = true;
                        Dictionary<int, IManagedActivityViewModel> activityLookup = RawActivities.ToDictionary(x => x.Id);

                        foreach (UpdateDependentActivityModel updateModel in updateModels)
                        {
                            if (activityLookup.TryGetValue(updateModel.Id, out IManagedActivityViewModel? activity))
                            {
                                if (activity is IEditableObject editable)
                                {
                                    editable.BeginEdit();

                                    if (updateModel.IsNameEdited)
                                    {
                                        activity.Name = updateModel.Name;
                                    }
                                    if (updateModel.IsNotesEdited)
                                    {
                                        activity.Notes = updateModel.Notes;
                                    }
                                    if (updateModel.IsTargetWorkStreamsEdited)
                                    {
                                        activity.WorkStreamSelector.SetSelectedTargetWorkStreams([.. updateModel.TargetWorkStreams]);
                                    }
                                    if (updateModel.IsTargetResourcesEdited)
                                    {
                                        activity.ResourceSelector.SetSelectedTargetResources([.. updateModel.TargetResources]);
                                    }
                                    if (updateModel.IsTargetResourceOperatorEdited)
                                    {
                                        activity.TargetResourceOperator = updateModel.TargetResourceOperator;
                                    }
                                    if (updateModel.IsHasNoCostEdited)
                                    {
                                        activity.HasNoCost = updateModel.HasNoCost;
                                    }
                                    if (updateModel.IsHasNoBillingEdited)
                                    {
                                        activity.HasNoBilling = updateModel.HasNoBilling;
                                    }
                                    if (updateModel.IsHasNoEffortEdited)
                                    {
                                        activity.HasNoEffort = updateModel.HasNoEffort;
                                    }
                                    if (updateModel.IsHasNoRiskEdited)
                                    {
                                        activity.HasNoRisk = updateModel.HasNoRisk;
                                    }

                                    editable.EndEdit();
                                }
                            }
                        }
                    });

                    IsProjectScenarioUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void AddMilestone(IEnumerable<int> dependentActivityIds)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    if (!HasCompilationErrors)
                    {
                        // Check the upstream activities to be milestoned are all present.
                        IEnumerable<IManagedActivityViewModel> upstreamActivities = [.. RawActivities.Where(x => dependentActivityIds.Contains(x.Id))];
                        HashSet<int> upstreamActivityIds = [.. upstreamActivities.Select(x => x.Id)];

                        if (upstreamActivityIds.Count != 0)
                        {
                            int highestId = upstreamActivityIds.Max();
                            int milestoneDisplayOrder = upstreamActivities.DefaultIfEmpty().Max(x => x?.DisplayOrder ?? 0) + 1;

                            // Create the milestone activity
                            int milestoneId = AddManagedActivity(milestoneDisplayOrder);

                            IManagedActivityViewModel? milestoneActivity = RawActivities
                                .FirstOrDefault(x => x.Id == milestoneId);

                            if (milestoneActivity != null)
                            {
                                // Now go through all the downstream activities, whose dependencies
                                // contain the upstream activity IDs, and add the ID of the milestone.
                                // Be sure to exclude the upstream activities themselves to avoid
                                // circular dependencies.
                                IEnumerable<IManagedActivityViewModel> downstreamCompiledActivities = [.. RawActivities
                                    .Where(x => x.Dependencies.Intersect(upstreamActivityIds).Any())
                                    .Except(upstreamActivities)];

                                IEnumerable<IManagedActivityViewModel> downstreamPlanningActivities = [.. RawActivities
                                    .Where(x => x.PlanningDependencies.Intersect(upstreamActivityIds).Any())
                                    .Except(upstreamActivities)];

                                // Repopulate the selected downstream activities' dependencies.
                                // This time with the new milestone activity ID.
                                foreach (IManagedActivityViewModel downstreamActivity in downstreamCompiledActivities)
                                {
                                    m_VertexGraphCompiler.SetActivityDependencies(
                                        downstreamActivity.Id,
                                        [.. downstreamActivity.Dependencies, milestoneId],
                                        downstreamActivity.PlanningDependencies);
                                }

                                foreach (IManagedActivityViewModel downstreamActivity in downstreamPlanningActivities)
                                {
                                    m_VertexGraphCompiler.SetActivityDependencies(
                                        downstreamActivity.Id,
                                        downstreamActivity.Dependencies,
                                        [.. downstreamActivity.PlanningDependencies, milestoneId]);
                                }

                                // Finally, add the upstream activities' IDs as dependencies
                                // for the milestone activity.
                                m_VertexGraphCompiler.SetActivityDependencies(
                                    milestoneId, upstreamActivityIds, []);
                            }
                        }

                        IsProjectScenarioUpdated = true;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void UpdateActivityDisplayOrders()
        {
            lock (m_Lock)
            {
                // Mark the display order as it was left.
                for (int i = 0; i < OrderableActivities.Count; i++)
                {
                    OrderableActivities[i].DisplayOrder = i;
                }
            }
        }

        public void UpdateManagedActivityIds(IEnumerable<(int OldId, int NewId)> idMaps)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    ProjectScenarioModel projectScenarioModel = BuildProjectScenario();
                    Guid projectScenarioId = m_SettingService.ScenarioId;
                    string projectScenarioTitle = m_SettingService.ScenarioTitle;
                    projectScenarioModel = ProjectScenarioHelper.UpdateActivityIds(projectScenarioModel, [.. idMaps]);
                    ProcessProjectScenario(projectScenarioModel, projectScenarioId, projectScenarioTitle);

                    IsProjectScenarioUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void UpdateManagedResourceIds(IEnumerable<(int OldId, int NewId)> idMaps)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    ProjectScenarioModel projectScenarioModel = BuildProjectScenario();
                    Guid projectScenarioId = m_SettingService.ScenarioId;
                    string projectScenarioTitle = m_SettingService.ScenarioTitle;
                    projectScenarioModel = ProjectScenarioHelper.UpdateResourceIds(projectScenarioModel, [.. idMaps]);
                    ProcessProjectScenario(projectScenarioModel, projectScenarioId, projectScenarioTitle);

                    IsProjectScenarioUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void UpdateManagedWorkStreamIds(IEnumerable<(int OldId, int NewId)> idMaps)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    ProjectScenarioModel projectScenarioModel = BuildProjectScenario();
                    Guid projectScenarioId = m_SettingService.ScenarioId;
                    string projectScenarioTitle = m_SettingService.ScenarioTitle;
                    projectScenarioModel = ProjectScenarioHelper.UpdateWorkStreamIds(projectScenarioModel, [.. idMaps]);
                    ProcessProjectScenario(projectScenarioModel, projectScenarioId, projectScenarioTitle);

                    IsProjectScenarioUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ClearManagedActivities()
        {
            try
            {
                lock (m_Lock)
                {
                    m_Activities.Edit(activities =>
                    {
                        IsBusy = true;

                        foreach (IManagedActivityViewModel activity in RawActivities)
                        {
                            activity.Dispose();
                        }
                        activities.Clear();

                        m_VertexGraphCompiler.Reset();
                    });
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void SetActivityDuration(int activityId, int newDuration)
        {
            try
            {
                lock (m_Lock)
                {
                    IManagedActivityViewModel? activity = RawActivities.FirstOrDefault(a => a.Id == activityId);

                    if (activity is not IEditableObject editable)
                    {
                        return;
                    }

                    editable.BeginEdit();
                    activity.Duration = Math.Max(1, newDuration);
                    editable.EndEdit();

                    IsProjectScenarioUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void RunCompile()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    UpdateActivityDisplayOrders();

                    var availableResources = new List<IResource<int, int>>();
                    if (!ResourceSettings.AreDisabled)
                    {
                        availableResources.AddRange(ResourceSettings.Resources.OrderBy(x => x.Id).Select(m_Mapper.ToResource));
                    }

                    var workStreams = new List<IWorkStream<int>>();
                    workStreams.AddRange(WorkStreamSettings.WorkStreams.Select(m_Mapper.ToWorkStream));

                    IGraphCompilation<int, int, int, IDependentActivity> graphCompilation = m_VertexGraphCompiler.Compile(availableResources, workStreams);
                    HasCompilationErrors = graphCompilation.CompilationErrors.Any();
                    GraphCompilation = graphCompilation;

                    IsProjectScenarioUpdated = true;
                    HasStaleOutputs = false;
                    IsReadyToReviseTrackers = ReadyToRevise.No;
                    IsReadyToCompile = ReadyToCompile.No;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void RunAutoCompile()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    if (AutoCompile)
                    {
                        RunCompile();
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void RunTransitiveReduction()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    m_VertexGraphCompiler.Compile();
                    m_VertexGraphCompiler.TransitiveReduction();
                    RunCompile();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void BuildArrowGraph()
        {
            lock (m_Lock)
            {
                if (HasCompilationErrors)
                {
                    ArrowGraph = new ArrowGraphModel();
                }
                else
                {
                    ArrowGraph = m_GraphCompilationService.BuildArrowGraph(
                        GraphCompilation.DependentActivities.Select(x => (IDependentActivity)x.CloneObject()));
                }
            }
        }

        public void BuildVertexGraph()
        {
            lock (m_Lock)
            {
                if (HasCompilationErrors)
                {
                    VertexGraph = new VertexGraphModel();
                }
                else
                {
                    VertexGraph = m_GraphCompilationService.BuildVertexGraph(
                        GraphCompilation.DependentActivities.Select(x => (IDependentActivity)x.CloneObject()),
                        ResourceSettings.Resources,
                        ResourceSettings.AreDisabled,
                        WorkStreamSettings.WorkStreams);
                }
            }
        }

        public void BuildResourceSeriesSet()
        {
            lock (m_Lock)
            {
                ResourceSeriesSet = m_ResourceSchedulingService.BuildResourceSeriesSet(
                    GraphCompilation,
                    ResourceSettings);
            }
        }

        public void BuildTrackingSeriesSet()
        {
            lock (m_Lock)
            {
                IList<ActivityModel> activityModels =
                    [.. RawActivities.Cast<ManagedActivityViewModel>().Select(m_Mapper.ToActivityModel)];

                TrackingSeriesSet = m_ResourceSchedulingService.BuildTrackingSeriesSet(
                    activityModels,
                    ResourceSettings,
                    HasResources);
            }
        }

        public void BuildNetworkMetrics()
        {
            lock (m_Lock)
            {
                NetworkMetrics = m_MetricCalculationService.BuildNetworkMetrics(
                    GraphCompilation,
                    HasCompilationErrors,
                    ProjectStart,
                    m_VertexGraphCompiler.StartTime,
                    m_VertexGraphCompiler.FinishTime);
                this.RaisePropertyChanged(nameof(Metrics));
            }
        }

        public void BuildRiskMetrics()
        {
            lock (m_Lock)
            {
                RiskMetrics = m_MetricCalculationService.BuildRiskMetrics(
                    GraphCompilation,
                    HasCompilationErrors,
                    GraphSettings.ActivitySeverities);
                this.RaisePropertyChanged(nameof(Metrics));
            }
        }

        public void BuildFinancialMetrics()
        {
            lock (m_Lock)
            {
                (CostsModel costs, BillingsModel billings, MarginsModel margins, EffortsModel efforts) =
                    m_MetricCalculationService.BuildFinancialMetrics(
                        ResourceSeriesSet,
                        HasCompilationErrors);

                CostMetrics = costs;
                BillingMetrics = billings;
                MarginMetrics = margins;
                EffortMetrics = efforts;
                this.RaisePropertyChanged(nameof(Metrics));
            }
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_ReadOnlyActivitiesSub?.Dispose();
            m_OrderableActivitiesSub?.Dispose();
            m_NetworkMetricsSub?.Dispose();
            m_AreActivitiesUncompiledSub?.Dispose();
            m_CompileOnSettingsUpdateSub?.Dispose();
            m_BuildArrowGraphSub?.Dispose();
            m_BuildVertexGraphSub?.Dispose();
            m_BuildResourceSeriesSetSub?.Dispose();
            m_BuildTrackingSeriesSetSub?.Dispose();
            m_BuildRiskMetricsSub?.Dispose();
            m_BuildFinancialMetricsSub?.Dispose();
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
                KillSubscriptions();
                m_ProjectFinish?.Dispose();
                m_HasActivities?.Dispose();
                m_HasResources?.Dispose();
                m_HasWorkStreams?.Dispose();
                m_HasPhases?.Dispose();
                ClearManagedActivities();
                m_Activities?.Dispose();
                m_DisplaySettingsViewModel?.Dispose();
            }

            m_Disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
