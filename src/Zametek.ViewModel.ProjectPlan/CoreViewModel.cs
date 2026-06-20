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
        private int m_BulkUpdateNestingLevel;
        private int m_BusyNestingLevel;

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
            m_ArrowGraphLayout = new GraphLayoutModel();
            m_VertexGraphLayout = new GraphLayoutModel();
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
                // Drop emissions raised during a bulk update: the bulk update methods
                // run the compilation explicitly, so these emissions are redundant.
                // Note this must come after the DynamicData operators (so their
                // internal state stays consistent) and before ObserveOn (so the check
                // runs at emission time, not at deferred delivery time).
                .Where(_ => !IsBulkUpdating)
                .ObserveOn(RxApp.TaskpoolScheduler)
                //.ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(changeSet =>
                {
                    if ((changeSet.Replaced + changeSet.Adds) > 0)
                    {
                        lock (m_Lock)
                        {
                            if (!IsBusy)
                            {
                                // The changeset's uncompiled verdict is baked in at
                                // emission time and may be stale by the time it is
                                // delivered here (e.g. activities added during a load
                                // are emitted as uncompiled, but the load has already
                                // compiled them by the time the load releases m_Lock),
                                // so re-check the live state before arming a redundant
                                // compile, which would mark the project scenario as
                                // updated.
                                if (RawActivities.Any(activity => !activity.IsCompiled))
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

            // The internal Build* subscriptions drop (rather than conflate) emissions
            // raised during a bulk update: the bulk update methods invoke the Build*
            // cascade actively, in dependency order, inside the bulk update window so
            // that all compilation outputs are settled before the gated manager view
            // model subscriptions replay. The drop check must run at emission time
            // (i.e. before ObserveOn), otherwise the deferred taskpool invocations
            // would run after the bulk update window has already closed.
            m_BuildArrowGraphSub = this
                .WhenAnyValue(core => core.GraphCompilation)
                .Where(_ => !IsBulkUpdating)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildArrowGraph());

            m_BuildVertexGraphSub = this
                .WhenAnyValue(core => core.GraphCompilation)
                .Where(_ => !IsBulkUpdating)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildVertexGraph());

            m_BuildResourceSeriesSetSub = this
                .WhenAnyValue(core => core.GraphCompilation)
                .Where(_ => !IsBulkUpdating)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildResourceSeriesSet());

            m_BuildTrackingSeriesSetSub = this
                .WhenAnyValue(core => core.GraphCompilation)
                .Where(_ => !IsBulkUpdating)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildTrackingSeriesSet());

            m_NetworkMetricsSub = this
                .WhenAnyValue(
                    core => core.GraphCompilation,
                    core => core.HasCompilationErrors)
                .Where(_ => !IsBulkUpdating)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildNetworkMetrics());

            m_BuildRiskMetricsSub = this
                .WhenAnyValue(
                    core => core.GraphCompilation,
                    core => core.GraphSettings,
                    core => core.HasCompilationErrors)
                .Where(_ => !IsBulkUpdating)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildRiskMetrics());

            m_BuildFinancialMetricsSub = this
                .WhenAnyValue(
                    core => core.ResourceSeriesSet,
                    core => core.HasCompilationErrors)
                .Where(_ => !IsBulkUpdating)
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

        /// <summary>
        /// Marks the start of a bulk update (e.g. loading or resetting a project scenario),
        /// during which subscribers gated on IsBulkUpdating should suppress reacting to
        /// intermediate property change notifications. Calls may be nested; IsBulkUpdating
        /// only raises change notifications on the outermost transitions.
        /// Always pair with EndBulkUpdate in a finally block.
        /// IMPORTANT: never call this, or EndBulkUpdate, while holding m_Lock. Raising
        /// IsBulkUpdating causes its (deliberately lock-free) getter to be re-read
        /// synchronously by property chain observers on this thread, and the bulk update
        /// state must never couple those observers to m_Lock, otherwise the raising
        /// thread can deadlock against the Build* subscriptions that serialize on m_Lock.
        /// </summary>
        private void BeginBulkUpdate()
        {
            if (Interlocked.Increment(ref m_BulkUpdateNestingLevel) == 1)
            {
                CascadeDiagnostics.RecordMarker(@"Bulk update started");
                this.RaisePropertyChanged(nameof(IsBulkUpdating));
            }
        }

        private void EndBulkUpdate()
        {
            if (Interlocked.Decrement(ref m_BulkUpdateNestingLevel) == 0)
            {
                CascadeDiagnostics.RecordMarker(@"Bulk update ended");
                this.RaisePropertyChanged(nameof(IsBulkUpdating));
            }
        }

        /// <summary>
        /// Marks the start of a busy section. IsBusy is a UI-facing signal only (busy
        /// indicators, disabled controls); do not use it to gate behaviour - use
        /// IsBulkUpdating or dedicated flags for that. Calls nest: the busy sections
        /// in this class routinely call each other, so IsBusy is ref-counted and only
        /// raises change notifications on the outermost transitions. This keeps the
        /// UI signal as one continuous busy window per operation instead of flapping
        /// false/true as each nested helper completes.
        /// Always pair with EndBusy in a finally block.
        /// </summary>
        private void BeginBusy()
        {
            if (Interlocked.Increment(ref m_BusyNestingLevel) == 1)
            {
                this.RaisePropertyChanged(nameof(IsBusy));
            }
        }

        private void EndBusy()
        {
            // Decrement defensively, clamping at zero: pairing is expected (BeginBusy
            // at the start of a try block, EndBusy in its finally), but an unmatched
            // call must never drive the count negative, which would silently break
            // the busy signal for the remainder of the session.
            //
            // This is a standard lock-free compare-and-swap retry loop. The initial
            // volatile read is only a first guess and may be stale by the time the
            // CompareExchange runs; that is fine, because CompareExchange writes the
            // decrement only if the field still holds the guessed value, atomically,
            // and otherwise writes nothing and returns the actual current value, with
            // which the loop simply tries again. A stale read can therefore only ever
            // cause a retry, never an incorrect write.
            int currentLevel = Volatile.Read(ref m_BusyNestingLevel);
            while (currentLevel > 0)
            {
                int originalLevel = Interlocked.CompareExchange(ref m_BusyNestingLevel, currentLevel - 1, currentLevel);
                if (originalLevel == currentLevel)
                {
                    if (currentLevel == 1)
                    {
                        this.RaisePropertyChanged(nameof(IsBusy));
                    }
                    return;
                }
                currentLevel = originalLevel;
            }
        }

        /// <summary>
        /// Actively runs the Build* cascade, in dependency order, that the internal
        /// subscriptions would otherwise perform in response to a compilation change.
        /// Call this inside a bulk update window (after RunCompile), when those
        /// subscriptions drop their emissions, so that all compilation outputs are
        /// settled before the bulk update ends.
        /// </summary>
        private void RunBuildCascade()
        {
            lock (m_Lock)
            {
                BuildArrowGraph();
                BuildVertexGraph();
                BuildResourceSeriesSet();
                BuildTrackingSeriesSet();
                BuildNetworkMetrics();
                BuildRiskMetrics();
                BuildFinancialMetrics();
            }
        }

        #endregion

        #region ICoreViewModel Members

        // This getter must remain lock-free: it is re-read synchronously by property
        // chain observers (WhenAnyValue) on whichever thread raises the change
        // notification, so it must never contend for m_Lock.
        public bool IsBusy => Volatile.Read(ref m_BusyNestingLevel) > 0;

        // This getter must remain lock-free: it is re-read synchronously by property
        // chain observers (WhenAnyValue) on whichever thread raises the change
        // notification, so it must never contend for m_Lock.
        public bool IsBulkUpdating => Volatile.Read(ref m_BulkUpdateNestingLevel) > 0;

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
                        if (value && !m_IsProjectScenarioUpdated)
                        {
                            CascadeDiagnostics.RecordStackTrace($@"{nameof(IsProjectScenarioUpdated)} transitioned to true");
                        }
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

        // The persisted interactive graph layouts (node positions, layout space). Settable so a loaded
        // scenario can rehydrate them and a node drag can update them; setting marks the scenario
        // modified (gated during a load/reset, like the other scenario state). The graph view-models
        // seed from these when their graph is (re)built and push the live arrangement back on a drag.
        private GraphLayoutModel m_ArrowGraphLayout;
        public GraphLayoutModel ArrowGraphLayout
        {
            get => m_ArrowGraphLayout;
            set
            {
                lock (m_Lock)
                {
                    m_ArrowGraphLayout = value;
                    SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                }
            }
        }

        private GraphLayoutModel m_VertexGraphLayout;
        public GraphLayoutModel VertexGraphLayout
        {
            get => m_VertexGraphLayout;
            set
            {
                lock (m_Lock)
                {
                    m_VertexGraphLayout = value;
                    SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
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
                    BeginBusy();

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
                EndBusy();
            }
        }

        public void ClearSettings()
        {
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();
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
                EndBusy();
            }
        }

        public void ResetProjectScenario()
        {
            try
            {
                BeginBulkUpdate();
                lock (m_Lock)
                {
                    BeginBusy();
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
                    ArrowGraphLayout = new();
                    VertexGraphLayout = new();
                    ResourceSeriesSet = new();
                    TrackingSeriesSet = new();

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
                EndBusy();
                EndBulkUpdate();
            }
        }

        public ProjectScenarioImportModel ImportProjectScenarioFile(string filename)
        {
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();
                    return m_ProjectScenarioFileImport.ImportProjectScenarioFile(filename);
                }
            }
            finally
            {
                EndBusy();
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
                    BeginBusy();
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
                EndBusy();
            }
        }

        public void ProcessProjectScenarioImport(
            ProjectScenarioImportModel projectScenarioImportModel,
            Guid projectScenarioId,
            string projectScenarioTitle)
        {
            try
            {
                BeginBulkUpdate();
                lock (m_Lock)
                {
                    BeginBusy();
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

                    // The internal Build* subscriptions drop their emissions during a
                    // bulk update, so run the cascade actively while everything is
                    // still muted.
                    RunBuildCascade();

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
                EndBusy();
                EndBulkUpdate();
            }
        }

        public void ProcessProjectScenario(
            ProjectScenarioModel projectScenarioModel,
            Guid projectScenarioId,
            string projectScenarioTitle)
        {
            try
            {
                BeginBulkUpdate();
                lock (m_Lock)
                {
                    BeginBusy();
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

                    // Interactive graph layouts (node positions). Set before the compile/build cascade so
                    // the graph view-models seed them when their graphs are (re)built.
                    ArrowGraphLayout = projectScenarioModel.ArrowGraphLayout;
                    VertexGraphLayout = projectScenarioModel.VertexGraphLayout;

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

                    // The internal Build* subscriptions drop their emissions during a
                    // bulk update, so run the cascade actively while everything is
                    // still muted.
                    RunBuildCascade();

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
                EndBusy();
                EndBulkUpdate();
            }
        }

        public ProjectScenarioModel BuildProjectScenario()
        {
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();
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
                        ArrowGraphLayout = ArrowGraphLayout,
                        VertexGraphLayout = VertexGraphLayout,
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
                EndBusy();
            }
        }

        public int AddManagedActivity(int displayOrder)
        {
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();
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
                EndBusy();
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
                        BeginBusy();

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
                EndBusy();
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
                        BeginBusy();
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
                EndBusy();
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
                        BeginBusy();
                        Dictionary<int, IManagedActivityViewModel> activityLookup = RawActivities.ToDictionary(x => x.Id);

                        foreach (UpdateDependentActivityModel updateModel in updateModels)
                        {
                            if (activityLookup.TryGetValue(updateModel.Id, out IManagedActivityViewModel? activity))
                            {
                                if (activity is IEditableObject editable)
                                {
                                    activity.IsEditMuted = true;
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
                                    if (updateModel.IsOverrideColorEdited)
                                    {
                                        activity.OverrideColor = updateModel.OverrideColor;
                                    }
                                    if (updateModel.IsColorFormatEdited)
                                    {
                                        activity.ColorFormat = updateModel.ColorFormat;
                                    }

                                    editable.EndEdit();
                                    activity.IsEditMuted = false;
                                }
                            }
                        }
                    });

                    IsProjectScenarioUpdated = true;

                    if (AutoCompile)
                    {
                        IsReadyToReviseTrackers = ReadyToRevise.Yes;
                        IsReadyToCompile = ReadyToCompile.Yes;
                    }
                }
            }
            finally
            {
                EndBusy();
            }
        }

        public void AddMilestone(IEnumerable<int> dependentActivityIds)
        {
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();

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
                EndBusy();
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
                    BeginBusy();

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
                EndBusy();
            }
        }

        public void UpdateManagedResourceIds(IEnumerable<(int OldId, int NewId)> idMaps)
        {
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();

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
                EndBusy();
            }
        }

        public void UpdateManagedWorkStreamIds(IEnumerable<(int OldId, int NewId)> idMaps)
        {
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();

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
                EndBusy();
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
                        BeginBusy();

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
                EndBusy();
            }
        }

        public void SetActivityDuration(int activityId, int newDuration)
        {
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();
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
                EndBusy();
            }
        }

        public void RunCompile()
        {
            CascadeDiagnostics.RecordStackTrace($@"{nameof(CoreViewModel)}.{nameof(RunCompile)} invoked");
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();

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
                EndBusy();
            }
        }

        public void RunAutoCompile()
        {
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();
                    if (AutoCompile)
                    {
                        RunCompile();
                    }
                }
            }
            finally
            {
                EndBusy();
            }
        }

        public void RunTransitiveReduction()
        {
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();
                    m_VertexGraphCompiler.Compile();
                    m_VertexGraphCompiler.TransitiveReduction();
                    RunCompile();
                }
            }
            finally
            {
                EndBusy();
            }
        }

        public void BuildArrowGraph()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(BuildArrowGraph)}");
            lock (m_Lock)
            {
                if (HasCompilationErrors)
                {
                    ArrowGraph = new ArrowGraphModel();
                }
                else
                {
                    // Stamp each event with a stable, activity-anchored id (ArrowEventIdMapper), so the
                    // interactive layout can persist and rehydrate by it; the compiler's own event ids
                    // are transient (regenerated every compile) and so cannot key a saved arrangement.
                    var arrowGraph = m_GraphCompilationService.BuildArrowGraph(
                        GraphCompilation.DependentActivities.Select(x => (IDependentActivity)x.CloneObject()));

                    ArrowGraph = ArrowEventIdMapper.ApplyStableIds(arrowGraph);
                }
            }
        }

        public void BuildVertexGraph()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(BuildVertexGraph)}");
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
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(BuildResourceSeriesSet)}");
            lock (m_Lock)
            {
                ResourceSeriesSet = m_ResourceSchedulingService.BuildResourceSeriesSet(
                    GraphCompilation,
                    ResourceSettings);
            }
        }

        public void BuildTrackingSeriesSet()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(BuildTrackingSeriesSet)}");
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
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(BuildNetworkMetrics)}");
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
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(BuildRiskMetrics)}");
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
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(BuildFinancialMetrics)}");
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
