using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
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

        // CompilationOutputRevision is an opaque change pulse, so it wraps to
        // zero at a deliberate boundary instead of overflowing. Any modulus >= 2
        // keeps consecutive raises distinct, which is all the property pipeline
        // needs (consecutive equal values would be de-duplicated downstream).
        private const int c_CompilationOutputRevisionWrap = 1_000_000;

        private readonly Lock m_Lock;

        private bool m_TrackIsProjectScenarioUpdated;
        private bool m_TrackHasStaleOutputs;
        private readonly NestingCounter m_BulkUpdateNesting = new();
        private readonly NestingCounter m_BusyNesting = new();

        private readonly CompilationScheduler m_Scheduler;

        // The compilation that the outputs were last built from. Used under m_Lock only.
        private IGraphCompilation<int, int, int, IDependentActivity>? m_CascadedCompilation;

        private readonly PlanGraph m_PlanGraph;

        private readonly IProjectScenarioFileImport m_ProjectScenarioFileImport;
        private readonly IProjectScenarioFileExport m_ProjectScenarioFileExport;
        private readonly ISettingService m_SettingService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly ProjectPlanMapper m_Mapper;
        private readonly CompilationOutputService m_OutputService;
        private readonly IDataGridScrollManager m_DataGridScrollManager;
        private readonly ILogger<CoreViewModel> m_Logger;

        // The reactive pipelines, made by StartSubscriptions and not by the constructor: see IStartSubscriptions.
        private readonly SubscriptionLifetime m_SubscriptionLifetime;
        private IDisposable? m_AreActivitiesUncompiledSub;
        private IDisposable? m_CompileOnSettingsUpdateSub;
        private IDisposable? m_BuildCascadeSub;
        private IDisposable? m_BuildRiskMetricsSub;

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
            IMetricCalculationService metricCalculationService,
            IDataGridScrollManager dataGridScrollManager,
            ILogger<CoreViewModel> logger)
        {
            ArgumentNullException.ThrowIfNull(projectScenarioFileImport);
            ArgumentNullException.ThrowIfNull(projectScenarioFileExport);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(graphCompilationService);
            ArgumentNullException.ThrowIfNull(resourceSchedulingService);
            ArgumentNullException.ThrowIfNull(metricCalculationService);
            ArgumentNullException.ThrowIfNull(dataGridScrollManager);
            ArgumentNullException.ThrowIfNull(logger);
            m_Logger = logger;
            m_Lock = new();
            m_Scheduler = new();
            m_SubscriptionLifetime = new();
            m_TrackIsProjectScenarioUpdated = true;
            m_TrackHasStaleOutputs = true;
            m_ProjectScenarioFileImport = projectScenarioFileImport;
            m_ProjectScenarioFileExport = projectScenarioFileExport;
            m_SettingService = settingService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_DisplaySettingsViewModel = new ProjectScenarioDisplaySettingsViewModel(
                m_DateTimeCalculator,
                SetIsProjectScenarioUpdated,
                () => IsReadyToCompile = ReadyToCompile.Yes,
                RefreshActivityDateTimes);
            m_Mapper = mapper;
            m_OutputService = new CompilationOutputService(
                graphCompilationService,
                resourceSchedulingService,
                metricCalculationService);
            m_DataGridScrollManager = dataGridScrollManager;

            m_IsReadyToCompile = ReadyToCompile.No;
            m_HasStaleOutputs = false;
            DateTimeOffset today = m_DateTimeCalculator.GetLocalToday();
            m_ProjectStart = today;
            m_Today = today;
            m_ResourceSettings = new ResourceSettingsModel();
            m_PlanGraph = new PlanGraph(m_Mapper, m_DateTimeCalculator, m_SubscriptionLifetime);
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
            m_ResourceMetrics = [];
            m_SimulationSettings = new();

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

            m_SelectedTheme = m_SettingService.SelectedTheme;

            // The finish, as numbers and as text. Each has a pipeline of its own - they depend on one another through
            // nothing but the plan - and the text is the numbers, shown as the display settings ask.
            m_ProjectFinishDays = this
                .WhenAnyValue(core => core.NetworkMetrics, (NetworkModel networkModel) => GetProjectFinishDays(networkModel))
                .ToProperty(this, core => core.ProjectFinishDays);

            m_ProjectFinishDate = this
                .WhenAnyValue(
                    core => core.ProjectStart,
                    core => core.NetworkMetrics,
                    core => core.HolidaySettings,
                    core => core.m_DateTimeCalculator.NonWorkingDayMode,
                    core => core.m_DateTimeCalculator.DisplayMode,
                    (DateTimeOffset projectStart, NetworkModel networkModel, HolidaySettingsModel _, NonWorkingDayMode _, DateTimeDisplayMode _) =>
                        GetProjectFinishDays(networkModel) is int days
                            ? DateOnly.FromDateTime(CalculateProjectFinish(projectStart, days).DateTime)
                            : (DateOnly?)null)
                .ToProperty(this, core => core.ProjectFinishDate);

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
                        if (GetProjectFinishDays(networkModel) is not int days)
                        {
                            return string.Empty;
                        }

                        return showDates
                            ? CalculateProjectFinish(projectStart, days).ToString(DateTimeCalculator.DateFormat)
                            : days.ToString();
                    })
                .ToProperty(this, mm => mm.ProjectFinish);

            m_HasActivities = Activities
                .ToObservableChangeSet()
                .Select(x => Activities.Count > 0)
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
        }

        #endregion

        #region Private Methods

        // The days from the project's start to its finish: the network's duration, or null when it has none to finish
        // after.
        private static int? GetProjectFinishDays(NetworkModel networkModel)
        {
            return networkModel.Duration is int duration && duration != 0
                ? duration
                : null;
        }

        // When the project finishes, as the display shows it: the day the last of its days ends on, in the working
        // calendar the settings give.
        private DateTimeOffset CalculateProjectFinish(
            DateTimeOffset projectStart,
            int days)
        {
            DateTimeOffset startAndFinish = m_DateTimeCalculator.AddDays(projectStart, days);
            return m_DateTimeCalculator.DisplayFinishDate(startAndFinish, startAndFinish, 1);
        }

        /// <summary>
        /// Recounts every activity's date-derived compiler inputs against the current
        /// working calendar. Invoked wherever that calendar changes - the holiday
        /// settings here, and the non-working day mode from the display settings, which
        /// owns it - so the recount is complete before the same caller arms a compile.
        /// </summary>
        /// <remarks>
        /// Deliberately lock-free. The display settings view model calls this from a
        /// setter that may still be inside its own lock (SetValues holds it across the
        /// whole batch), and taking m_Lock there would add a display-settings to core
        /// lock-order edge of exactly the kind that deadlocked against
        /// ProcessProjectScenario, which holds m_Lock while calling into that class.
        /// Nothing here needs the lock: reading the activities takes a snapshot of the
        /// source list, and each activity's recount takes only the leaf activity data
        /// lock. An activity added concurrently is not missed either, since it counts
        /// its own times from the current calendar as it is built.
        /// </remarks>
        private void RefreshActivityDateTimes()
        {
            m_PlanGraph.UpdateEarliestStartAndLatestFinishDateTimes();
        }

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
            if (m_BulkUpdateNesting.Begin())
            {
                CascadeDiagnostics.RecordMarker(@"Bulk update started");
                this.RaisePropertyChanged(nameof(IsBulkUpdating));
            }
        }

        private void EndBulkUpdate()
        {
            if (m_BulkUpdateNesting.End())
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
            if (m_BusyNesting.Begin())
            {
                this.RaisePropertyChanged(nameof(IsBusy));
            }
        }

        private void EndBusy()
        {
            if (m_BusyNesting.End())
            {
                this.RaisePropertyChanged(nameof(IsBusy));
            }
        }

        /// <summary>
        /// Runs the Build* cascade, in dependency order, then bumps
        /// <see cref="CompilationOutputRevision"/> as the settled signal. The
        /// reactive pipeline invokes this once per compilation change; the bulk
        /// update methods invoke it actively (after RunCompile) while their
        /// subscriptions are muted, so that all compilation outputs are settled
        /// before the bulk update ends.
        /// </summary>
        /// <remarks>
        /// The outputs are built from a copy of what they read, taken in one short step
        /// under <see cref="m_Lock"/>, and they are built without it - the cascade takes
        /// longer than the compilation that it follows. They are published together, in
        /// another short step, and only if the compilation they were built from is still
        /// the current one. When a later compilation has published in the meantime, its
        /// own cascade is on its way, and publishing these would show the user outputs
        /// that are already out of date (ARCHITECTURE section 7 rule 14).
        /// <para>
        /// A bulk update calls this while it holds the lock for the whole of the update,
        /// and then all of it runs under that hold, as it always did.
        /// </para>
        /// </remarks>
        private void RunBuildCascade()
        {
            try
            {
                CascadeInputs inputs;

                lock (m_Lock)
                {
                    // A burst of compilations queues a cascade for each of them behind the one
                    // that is running. The first of the queued cascades builds from the newest
                    // compilation, and so the others find nothing to add.
                    if (ReferenceEquals(m_CascadedCompilation, GraphCompilation))
                    {
                        return;
                    }

                    inputs = CaptureCascadeInputs();
                }

                CascadeOutputs outputs = m_OutputService.Compute(inputs);

                lock (m_Lock)
                {
                    if (m_Disposed
                        || !ReferenceEquals(inputs.GraphCompilation, GraphCompilation)
                        || inputs.ScenarioRevision != ProjectScenarioRevision)
                    {
                        return;
                    }

                    ApplyCascadeOutputs(outputs);
                    m_CascadedCompilation = inputs.GraphCompilation;
                    CompilationOutputRevision =
                        (CompilationOutputRevision + 1) % c_CompilationOutputRevisionWrap;
                }
            }
            catch (GraphCompilationTimeoutException ex)
            {
                // The arrow and vertex display graphs are built by compiling the
                // plan again, so they carry the same watchdog and can abandon the
                // cascade part way through. None of the outputs has been published
                // by then, because they are published together after the last of
                // them is built; the revision pulse is deliberately not bumped, so
                // subscribers waiting on a settled set of outputs do not act on a
                // half-built one.
                AbandonCompilation(ex);
                throw;
            }
        }

        /// <summary>
        /// Copies what the outputs of a cascade read. Call it with <see cref="m_Lock"/> held, after the
        /// compilation has published: the tracking series reads the times that it published.
        /// </summary>
        private CascadeInputs CaptureCascadeInputs() =>
            new(
                GraphCompilation,
                HasCompilationErrors,
                ResourceSettings,
                WorkStreamSettings,
                [.. GraphSettings.ActivitySeverities],
                ProjectStart,
                m_PlanGraph.StartTime,
                m_PlanGraph.FinishTime,
                HasResources,
                MapActivityModels(),
                ProjectScenarioRevision);

        private IList<ActivityModel> MapActivityModels() =>
            [.. RawActivities.Cast<ManagedActivityViewModel>().Select(m_Mapper.ToActivityModel)];

        /// <summary>
        /// Publishes the outputs of a cascade, in the order that they were built in. Call it with
        /// <see cref="m_Lock"/> held.
        /// </summary>
        private void ApplyCascadeOutputs(CascadeOutputs outputs)
        {
            ArrowGraph = outputs.ArrowGraph;
            VertexGraph = outputs.VertexGraph;
            ResourceSeriesSet = outputs.ResourceSeriesSet;
            TrackingSeriesSet = outputs.TrackingSeriesSet;
            ApplyNetworkMetrics(outputs.NetworkMetrics);
            ApplyRiskMetrics(outputs.RiskMetrics);
            ApplyFinancialMetrics(
                outputs.CostMetrics,
                outputs.BillingMetrics,
                outputs.MarginMetrics,
                outputs.EffortMetrics,
                outputs.ResourceMetrics);
        }

        /// <summary>
        /// Takes the copy of the plan that a compilation works on. Call it with <see cref="m_Lock"/> held: it
        /// reads the live graph, which the structural edits change under that lock.
        /// </summary>
        private CompilationSnapshot TakeSnapshot()
        {
            UpdateActivityDisplayOrders();

            var availableResources = new List<IResource<int, int>>();
            if (!ResourceSettings.AreDisabled)
            {
                availableResources.AddRange(ResourceSettings.Resources.OrderBy(x => x.Id).Select(m_Mapper.ToResource));
            }

            var workStreams = new List<IWorkStream<int>>();
            workStreams.AddRange(WorkStreamSettings.WorkStreams.Select(m_Mapper.ToWorkStream));

            return m_Scheduler.TakeSnapshot(availableResources, workStreams, ProjectScenarioRevision, m_PlanGraph.CopyActivities);
        }

        /// <summary>
        /// Compiles the copy of the plan. Here so that a test can hold a compilation part way through, to see what
        /// happens to the edits that are made meanwhile.
        /// </summary>
        protected virtual IGraphCompilation<int, int, int, IDependentActivity> CompilePlan(
            VertexGraphCompiler compiler,
            List<IResource<int, int>> availableResources,
            List<IWorkStream<int>> workStreams,
            CancellationToken cancellationToken) =>
            compiler.Compile(availableResources, workStreams, cancellationToken);

        // Publishing the metrics. Call these with m_Lock held. The setters mark the scenario as updated when a
        // value is different, and Metrics is raised for the observers of the record as a whole.

        private void ApplyNetworkMetrics(NetworkModel networkMetrics)
        {
            NetworkMetrics = networkMetrics;
            this.RaisePropertyChanged(nameof(Metrics));
        }

        private void ApplyRiskMetrics(RisksModel riskMetrics)
        {
            RiskMetrics = riskMetrics;
            this.RaisePropertyChanged(nameof(Metrics));
        }

        private void ApplyFinancialMetrics(
            CostsModel costs,
            BillingsModel billings,
            MarginsModel margins,
            EffortsModel efforts,
            List<ResourceMetricsModel> resourceMetrics)
        {
            CostMetrics = costs;
            BillingMetrics = billings;
            MarginMetrics = margins;
            EffortMetrics = efforts;
            ResourceMetrics = resourceMetrics;
            this.RaisePropertyChanged(nameof(Metrics));
        }

        /// <summary>
        /// Applies the state that follows a compilation the watchdog cut short. The
        /// last good compilation output is left alone and stays on display, but it no
        /// longer matches the plan, so the outputs are flagged stale. Automatic
        /// compilation is switched off as well, so that the next edit does not walk
        /// straight back into the same hang and put the same error up again; the user
        /// re-enables it, or compiles by hand, once they have changed something.
        /// </summary>
        /// <remarks>
        /// Always invoked after the busy signal has been released, so the UI is idle
        /// by the time the exception reaches whichever view model reports it.
        /// </remarks>
        private void AbandonCompilation(GraphCompilationTimeoutException ex)
        {
            m_Logger.LogError(
                ex,
                "Compilation was cancelled after {TimeoutMilliseconds}ms",
                ex.Timeout.TotalMilliseconds);

            HasStaleOutputs = true;
            IsReadyToCompile = ReadyToCompile.No;
            AutoCompile = false;
        }

        #endregion

        #region ICoreViewModel Members

        // This getter must remain lock-free: it is re-read synchronously by property
        // chain observers (WhenAnyValue) on whichever thread raises the change
        // notification, so it must never contend for m_Lock.
        public bool IsBusy => m_BusyNesting.IsActive;

        // This getter must remain lock-free: it is re-read synchronously by property
        // chain observers (WhenAnyValue) on whichever thread raises the change
        // notification, so it must never contend for m_Lock.
        public bool IsBulkUpdating => m_BulkUpdateNesting.IsActive;

        // We need to use an enum because raised changes on bools aren't always captured.
        // https://github.com/reactiveui/ReactiveUI/issues/3846
        private ReadyToCompile m_IsReadyToCompile;

        // This should always be the last thing altered in order to trigger a compile.
        public ReadyToCompile IsReadyToCompile
        {
            get => m_IsReadyToCompile;
            private set
            {
                // Every arm is a change of what a compilation reads, including the one that finds the property
                // armed already and so raises nothing. A compilation that is running counts it, and compiles again.
                if (value == ReadyToCompile.Yes)
                {
                    NoteInputsChanged();
                }

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
                        else if (!value && m_IsProjectScenarioUpdated)
                        {
                            CascadeDiagnostics.RecordStackTrace($@"{nameof(IsProjectScenarioUpdated)} transitioned to false");
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

        private int m_CompilationOutputRevision;
        /// <summary>
        /// Increments once each time the full Build* output cascade has settled
        /// after a compilation (or bulk load), so subscribers that need every
        /// output in place can react exactly once per compile. The value is an
        /// opaque change pulse that wraps to zero at
        /// <see cref="c_CompilationOutputRevisionWrap"/> - observe changes,
        /// never compare magnitudes.
        /// </summary>
        public int CompilationOutputRevision
        {
            get => m_CompilationOutputRevision;
            private set
            {
                m_CompilationOutputRevision = value;
                this.RaisePropertyChanged();
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

                    // Push the new start into every activity synchronously, so their
                    // minimum earliest start times and maximum latest finish times are
                    // measured from it before the compile armed below can read them.
                    // This used to happen through a per-activity subscription observing
                    // on the current thread's trampoline, which deferred the work
                    // whenever the write itself was made from inside another scheduled
                    // delivery. During a load this loop is empty: the start is always
                    // assigned before the activities are added, and each activity is
                    // built with the current start.
                    m_PlanGraph.SetProjectStart(m_ProjectStart);

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

        private readonly ObservableAsPropertyHelper<int?> m_ProjectFinishDays;
        public int? ProjectFinishDays => m_ProjectFinishDays.Value;

        private readonly ObservableAsPropertyHelper<DateOnly?> m_ProjectFinishDate;
        public DateOnly? ProjectFinishDate => m_ProjectFinishDate.Value;

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
                        // Switching automatic compilation on compiles nothing by itself: the next edit does.
                        IsReadyToCompile = ReadyToCompile.No;
                        m_Scheduler.AbsorbPendingInputs();
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

        public IReadOnlyList<IManagedActivityViewModel> RawActivities => m_PlanGraph.RawActivities;

        public ReadOnlyObservableCollection<IManagedActivityViewModel> Activities => m_PlanGraph.Activities;

        public ObservableCollection<IManagedActivityViewModel> OrderableActivities => m_PlanGraph.OrderableActivities;

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

                    // Push the new settings into every activity synchronously, so
                    // their target resource sets are reconciled before the compile
                    // armed below can observe them. This used to happen through a
                    // per-activity subscription deferred to the UI thread, which
                    // could clear and rebuild the live target sets while a compile
                    // on another thread was cloning them - the torn-HashSet
                    // corruption diagnosed from the zametek-deadlock-2 dump. During
                    // a load this loop is empty: the settings are always assigned
                    // before the activities are added, and each activity seeds
                    // itself from the current settings in its constructor.
                    m_PlanGraph.SetResourceSettings(value);

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

                    // Synchronous for the same reason as ResourceSettings above.
                    m_PlanGraph.SetWorkStreamSettings(value);

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

                    // Synchronous for the same reason as ProjectStart above: the holidays
                    // are the calendar those times are counted along, so changing them
                    // makes every activity's stored count wrong until it is redone.
                    RefreshActivityDateTimes();

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

        private List<ResourceMetricsModel> m_ResourceMetrics;
        public List<ResourceMetricsModel> ResourceMetrics
        {
            get => m_ResourceMetrics;
            private set
            {
                lock (m_Lock)
                {
                    // List identity is not value identity: compare the sequences
                    // (the entries are records, so they compare by value) so that
                    // a loaded list matching the compiled one does not raise a
                    // change or mark the scenario as updated.
                    if (m_ResourceMetrics == value
                        || m_ResourceMetrics.SequenceEqual(value))
                    {
                        return;
                    }
                    m_ResourceMetrics = value;
                    SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                }
            }
        }

        // The settings of the schedule simulation and its last result are the scenario's, as the metrics are. The
        // getters are lock-free: a property chain re-reads them on the thread that raises the change.
        private SimulationSettingsModel m_SimulationSettings;
        public SimulationSettingsModel SimulationSettings
        {
            get => Volatile.Read(ref m_SimulationSettings);
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                lock (m_Lock)
                {
                    if (m_SimulationSettings == value)
                    {
                        return;
                    }
                    Volatile.Write(ref m_SimulationSettings, value);
                    SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                }
            }
        }

        private SimulationSummaryModel? m_SimulationSummary;
        public SimulationSummaryModel? SimulationSummary
        {
            get => Volatile.Read(ref m_SimulationSummary);
            set
            {
                lock (m_Lock)
                {
                    if (ReferenceEquals(m_SimulationSummary, value))
                    {
                        return;
                    }
                    Volatile.Write(ref m_SimulationSummary, value);
                    SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                }
            }
        }

        private int m_ProjectScenarioRevision;
        public int ProjectScenarioRevision => Volatile.Read(ref m_ProjectScenarioRevision);

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
                return m_PlanGraph.GetNextActivityId();
            }
        }

        public ProjectScenarioModel CreateEmptyProjectScenario()
        {
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();

                    return ProjectScenarioHelper.CreateEmptyProjectScenario(m_DateTimeCalculator.GetLocalToday(), m_SettingService);
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

                    // Drop any cached datagrid scroll positions so grids return to the top
                    // whenever a project or project scenario is loaded or reset.
                    m_DataGridScrollManager.ClearScrollItems();

                    m_SettingService.ResetProjectScenario();

                    Metrics = new();
                    ResourceMetrics = [];

                    // The simulation belongs to the scenario that is gone. A run that is in progress sees the revision
                    // change, and stops.
                    SimulationSettings = new();
                    SimulationSummary = null;
                    Interlocked.Increment(ref m_ProjectScenarioRevision);
                    this.RaisePropertyChanged(nameof(ProjectScenarioRevision));

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

                    // The settings that were cleared above armed the compilation, and an empty scenario has nothing
                    // to compile. A compilation that is running finds the scenario changed, and discards its results.
                    m_Scheduler.AbsorbPendingInputs();

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

        public ProjectScenarioImportModel ImportProjectScenarioFile(Stream stream, ProjectScenarioImportFormat format)
        {
            try
            {
                lock (m_Lock)
                {
                    BeginBusy();
                    return m_ProjectScenarioFileImport.ImportProjectScenarioFile(stream, format);
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
            Stream stream,
            ProjectScenarioExportFormat format)
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
                        stream,
                        format);
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

                    HolidaySettings = ProjectScenarioHelper.CreateImportedHolidaySettings(m_SettingService.DefaultHolidaySettings, projectScenarioImportModel);

                    // Work Stream settings.
                    WorkStreamSettings = ProjectScenarioHelper.CreateImportedWorkStreamSettings(m_SettingService.DefaultWorkStreamSettings, projectScenarioImportModel);

                    // Resources.
                    ResourceSettings = ProjectScenarioHelper.CreateImportedResourceSettings(m_SettingService.DefaultResourceSettings, projectScenarioImportModel);

                    // Graph settings.
                    GraphSettings = ProjectScenarioHelper.CreateImportedGraphSettings(m_SettingService.DefaultGraphSettings, projectScenarioImportModel);

                    // Activities.
                    // Be sure to set the ResourceSettings first, so that the activities know
                    // which resources are being referred to when marking them as selected.
                    AddManagedActivities(projectScenarioImportModel.DependentActivities);

                    // Display settings.
                    DisplaySettingsViewModel.SetValues(projectScenarioImportModel.DisplaySettings);

                    // The simulation: its settings, and its last result if the workbook has one. The result is trusted,
                    // as the result that a project file holds is.
                    SimulationSettings = projectScenarioImportModel.SimulationSettings;
                    SimulationSummary = projectScenarioImportModel.SimulationSummary;

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
            // The single funnel for every scenario switch (and for the scenario loaded as
            // part of opening a project), so the log shows which scenario was in play when
            // anything after this point went wrong.
            m_Logger.LogInformation(
                "Loading scenario {ScenarioTitle} ({ScenarioId:D})",
                projectScenarioTitle,
                projectScenarioId);

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

                    // Files saved before the per-resource metrics existed carry an empty
                    // list, so only restore a stored list that actually has content -
                    // otherwise keep the values the compile above just produced, so the
                    // panel is populated on the first open of an older file.
                    if (projectScenarioModel.ResourceMetrics.Count != 0)
                    {
                        ResourceMetrics = projectScenarioModel.ResourceMetrics;
                    }

                    // The simulation: its settings, and its last result if the file has one. A result that a file holds
                    // is trusted, as the metrics above are, and thus the file does not say that it is out of date.
                    SimulationSettings = projectScenarioModel.SimulationSettings;
                    SimulationSummary = projectScenarioModel.SimulationSummary;

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
                        ResourceMetrics = [.. ResourceMetrics],
                        SimulationSettings = SimulationSettings,
                        SimulationSummary = SimulationSummary,
                        DisplaySettings = DisplaySettingsViewModel.GetValues(),
                        ArrowGraphLayout = ArrowGraphLayout.CloneObject(),
                        VertexGraphLayout = VertexGraphLayout.CloneObject(),
                    };

                    // Reorder activity dependencies so they are more readable.
                    ProjectScenarioHelper.SortDependencies(plan.DependentActivities);

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
                    BeginBusy();
                    m_PlanGraph.AddActivities(this, ProjectStart, dependentActivityModels);

                    UpdateActivityDisplayOrders();
                    NoteInputsChanged();
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
                    BeginBusy();
                    m_PlanGraph.RemoveActivities(dependentActivityIds);

                    UpdateActivityDisplayOrders();
                    NoteInputsChanged();
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
                    BeginBusy();
                    m_PlanGraph.UpdateActivities(updateModels);

                    NoteInputsChanged();
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
                                    m_PlanGraph.SetActivityDependencies(
                                        downstreamActivity.Id,
                                        [.. downstreamActivity.Dependencies, milestoneId],
                                        downstreamActivity.PlanningDependencies);
                                }

                                foreach (IManagedActivityViewModel downstreamActivity in downstreamPlanningActivities)
                                {
                                    m_PlanGraph.SetActivityDependencies(
                                        downstreamActivity.Id,
                                        downstreamActivity.Dependencies,
                                        [.. downstreamActivity.PlanningDependencies, milestoneId]);
                                }

                                // Finally, add the upstream activities' IDs as dependencies
                                // for the milestone activity.
                                m_PlanGraph.SetActivityDependencies(
                                    milestoneId, upstreamActivityIds, []);
                            }
                        }

                        NoteInputsChanged();
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
                m_PlanGraph.SetDisplayOrders();
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
                    BeginBusy();
                    m_PlanGraph.ClearActivities();
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

        public void ResetActivityEstimates(IEnumerable<int> activityIds)
        {
            ArgumentNullException.ThrowIfNull(activityIds);

            try
            {
                lock (m_Lock)
                {
                    BeginBusy();
                    HashSet<int> ids = [.. activityIds];

                    foreach (IManagedActivityViewModel activity in RawActivities.Where(x => ids.Contains(x.Id)))
                    {
                        // An activity whose estimates all follow its most likely duration has nothing to
                        // reset, and an edit that changed nothing must not mark the plan as updated.
                        if (!activity.HasOwnEstimates
                            || activity is not IEditableObject editable)
                        {
                            continue;
                        }

                        editable.BeginEdit();
                        activity.ResetEstimates();
                        editable.EndEdit();
                        IsProjectScenarioUpdated = true;
                    }
                }
            }
            finally
            {
                EndBusy();
            }
        }

        public SimulationInputModel CaptureSimulationInput()
        {
            lock (m_Lock)
            {
                // The resources and the work streams are the ones that a compile uses.
                var resources = new List<IResource<int, int>>();
                if (!ResourceSettings.AreDisabled)
                {
                    resources.AddRange(ResourceSettings.Resources.OrderBy(x => x.Id).Select(m_Mapper.ToResource));
                }

                List<IWorkStream<int>> workStreams = [.. WorkStreamSettings.WorkStreams.Select(m_Mapper.ToWorkStream)];

                (List<IDependentActivity<int, int, int>> activities, Dictionary<int, DurationEstimate> estimates, Dictionary<int, string> names) =
                    m_PlanGraph.CopySimulationActivities();

                return new SimulationInputModel
                {
                    Activities = activities,
                    Resources = resources,
                    WorkStreams = workStreams,
                    Estimates = estimates,
                    ActivityNames = names,
                    CompilationOutputRevision = CompilationOutputRevision,
                };
            }
        }

        /// <summary>
        /// Counts a change of something that a compilation reads. A compilation that is running did not see the
        /// change, so it publishes its results as stale, and the plan is compiled again.
        /// </summary>
        public void NoteInputsChanged() => m_Scheduler.NoteInputsChanged();

        public void RunCompile() => RunCompile(drainAfter: true);

        // A compilation is three steps: snapshot, compile, publish (ARCHITECTURE section 7 rules 11 and 14). Only the first
        // and the last hold m_Lock, and each of them is short. The compile in between takes the time, and it works on
        // private copies. So a compilation no longer holds up the edits of the plan, and an edit made meanwhile is
        // counted: the results are published as stale, and the plan is compiled again (see DrainAutoCompiles).
        //
        // A bulk update calls this while it holds m_Lock for the whole of the update. All three steps then run under
        // that hold, as they always did: it rewrites the model, and nothing else should see the model part way.
        private void RunCompile(bool drainAfter)
        {
            CascadeDiagnostics.RecordStackTrace($@"{nameof(CoreViewModel)}.{nameof(RunCompile)} invoked");

            bool isNested = m_Lock.IsHeldByCurrentThread;

            // Captured under the lock but logged after it: a compile runs on the taskpool
            // while UI bindings contend for this lock, so nothing avoidable happens inside.
            bool compilationErrorsAppeared = false;
            int compilationErrorCount = 0;

            int timeoutMilliseconds = m_SettingService.CompilationTimeoutMilliseconds;

            try
            {
                try
                {
                    BeginBusy();

                    CompilationSnapshot snapshot;

                    lock (m_Lock)
                    {
                        snapshot = TakeSnapshot();
                    }

                    IGraphCompilation<int, int, int, IDependentActivity> graphCompilation = m_Scheduler.Compile(snapshot, timeoutMilliseconds, CompilePlan);

                    lock (m_Lock)
                    {
                        // The results are of no use to anyone, and are dropped, when another scenario has been loaded
                        // since the snapshot, or when a snapshot taken after this one has published already. Publishing
                        // them would put the plan of one scenario into another, or an older plan over a newer one.
                        if (!m_Disposed
                            && snapshot.ScenarioRevision == ProjectScenarioRevision
                            && m_Scheduler.TryClaimPublication(snapshot))
                        {
                            m_PlanGraph.PublishCompilation(graphCompilation, snapshot.Sources);
                            bool hadCompilationErrors = HasCompilationErrors;
                            HasCompilationErrors = graphCompilation.CompilationErrors.Any();
                            compilationErrorsAppeared = HasCompilationErrors && !hadCompilationErrors;
                            compilationErrorCount = graphCompilation.CompilationErrors.Count();
                            GraphCompilation = graphCompilation;

                            IsProjectScenarioUpdated = true;

                            // The outputs match the plan only if nothing that a compilation reads has changed since the
                            // snapshot. If something has, these results are the newest there are and they stay on
                            // display, but they are stale until the compilation that follows has published.
                            HasStaleOutputs = m_Scheduler.HasMovedOnSince(snapshot);
                            IsReadyToReviseTrackers = ReadyToRevise.No;
                            IsReadyToCompile = ReadyToCompile.No;
                        }
                    }
                }
                finally
                {
                    EndBusy();
                }
            }
            catch (GraphCompilationTimeoutException ex)
            {
                // Nothing above assigned GraphCompilation, so the previous results
                // are still in place and stay on display; the outputs are simply
                // flagged as no longer matching the plan.
                AbandonCompilation(ex);
                throw;
            }

            // Only the transition into a failing state is recorded. A plan is usually
            // edited repeatedly while it will not compile, and logging every one of those
            // compiles would bury the moment it first broke.
            if (compilationErrorsAppeared)
            {
                m_Logger.LogWarning("Compilation failed with {CompilationErrorCount} error(s)", compilationErrorCount);
            }

            // A compilation leaves nothing due behind it. When the plan changed while this one ran, and automatic
            // compilation is on, the next compilation is made here. A bulk update does not: it holds the lock, and its
            // snapshot is taken after everything it changed.
            if (drainAfter && !isNested)
            {
                DrainAutoCompiles();
            }
        }

        public void RunAutoCompile()
        {
            CascadeDiagnostics.RecordMarker($@"RunAutoCompile invoked: AutoCompile={AutoCompile} IsBusy={IsBusy}");

            // A request for a compilation: the caller has changed the plan. Counting the request makes the plan due a
            // compilation, and this thread then makes it - unless another thread is making compilations already. That
            // thread looks at the revision again when it has published, and so it compiles for this request as well.
            NoteInputsChanged();
            DrainAutoCompiles();
        }

        // The compilations that the edits ask for are made by the scheduler, one thread at a time, and it shows one busy
        // window for all of them. Each of them is a call of RunCompile.
        private void DrainAutoCompiles() =>
            m_Scheduler.Drain(() => AutoCompile, BeginBusy, EndBusy, () => RunCompile(drainAfter: false));

        public void RunTransitiveReduction()
        {
            int timeoutMilliseconds = m_SettingService.CompilationTimeoutMilliseconds;

            try
            {
                try
                {
                    lock (m_Lock)
                    {
                        BeginBusy();

                        // The reduction works on a copy of the plan, and the plan shows nothing of it until it is done. The
                        // compilation of the copy checks the plan, and takes the dependencies that the resources gave out of
                        // the network. They are dynamic, and the reduction must see the dependencies of the user alone: it
                        // would take away a dependency of the user that only a resource implies.
                        (List<IDependentActivity> copies, Dictionary<int, IManagedActivityViewModel> sources) = m_PlanGraph.CopyActivities();
                        TransitiveReduction reduction = m_Scheduler.ReduceTransitively(copies, timeoutMilliseconds, CompilePlan);

                        if (reduction.Compilation.CompilationErrors.Any())
                        {
                            throw new InvalidOperationException(DescribeCompilationErrors(reduction.Compilation));
                        }

                        if (!m_PlanGraph.TryApplyReduction(reduction, sources))
                        {
                            throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_TransitiveReductionPlanChanged);
                        }

                        // The compilation publishes its results once, and with them the new dependencies reach the view.
                        RunCompile();
                    }
                }
                finally
                {
                    EndBusy();
                }
            }
            catch (GraphCompilationTimeoutException ex)
            {
                // Only reached when the compile above timed out; a timeout inside the
                // nested RunCompile has already been dealt with by RunCompile itself,
                // and repeating the state changes here would be harmless anyway.
                AbandonCompilation(ex);
                throw;
            }
        }

        // What the transitive reduction says when it is refused: why, and then the errors of the compilation one to a line.
        private static string DescribeCompilationErrors(IGraphCompilation<int, int, int, IDependentActivity> compilation) =>
            NewLineHelper.JoinLines(
            [
                Resource.ProjectPlan.Messages.Message_TransitiveReductionCompilationErrors,
                .. compilation.CompilationErrors.Select(x => $@"{x.ErrorCode}: {x.ErrorMessage}"),
            ]);

        public void BuildArrowGraph()
        {
            lock (m_Lock)
            {
                ArrowGraph = m_OutputService.ComputeArrowGraph(
                    GraphCompilation,
                    HasCompilationErrors);
            }
        }

        public void BuildVertexGraph()
        {
            lock (m_Lock)
            {
                VertexGraph = m_OutputService.ComputeVertexGraph(
                    GraphCompilation,
                    HasCompilationErrors,
                    ResourceSettings,
                    WorkStreamSettings);
            }
        }

        public void BuildResourceSeriesSet()
        {
            lock (m_Lock)
            {
                ResourceSeriesSet = m_OutputService.ComputeResourceSeriesSet(
                    GraphCompilation,
                    ResourceSettings);
            }
        }

        public void BuildTrackingSeriesSet()
        {
            lock (m_Lock)
            {
                TrackingSeriesSet = m_OutputService.ComputeTrackingSeriesSet(
                    MapActivityModels(),
                    ResourceSettings,
                    HasResources);
            }
        }

        public void BuildNetworkMetrics()
        {
            lock (m_Lock)
            {
                ApplyNetworkMetrics(m_OutputService.ComputeNetworkMetrics(
                    GraphCompilation,
                    HasCompilationErrors,
                    ProjectStart,
                    m_PlanGraph.StartTime,
                    m_PlanGraph.FinishTime));
            }
        }

        public void BuildRiskMetrics()
        {
            lock (m_Lock)
            {
                ApplyRiskMetrics(m_OutputService.ComputeRiskMetrics(
                    GraphCompilation,
                    HasCompilationErrors,
                    GraphSettings.ActivitySeverities));
            }
        }

        public void BuildFinancialMetrics()
        {
            lock (m_Lock)
            {
                (CostsModel costs, BillingsModel billings, MarginsModel margins, EffortsModel efforts, List<ResourceMetricsModel> resourceMetrics) =
                    m_OutputService.ComputeFinancialMetrics(
                        ResourceSeriesSet,
                        HasCompilationErrors);

                ApplyFinancialMetrics(costs, billings, margins, efforts, resourceMetrics);
            }
        }

        #endregion

        #region IStartSubscriptions Members

        public void StartSubscriptions()
        {
            if (!m_SubscriptionLifetime.TryStart())
            {
                return;
            }

            // The views of the source list that the UI binds to, filled on the UI thread.
            m_PlanGraph.StartBinds();

            m_AreActivitiesUncompiledSub = Activities
                .ToObservableChangeSet()
                .AutoRefresh(activity => activity.IsCompiled) // Subscribe only to IsCompiled property changes
                .Filter(activity => !activity.IsCompiled)
                // Drop emissions raised during a bulk update: the bulk update methods
                // run the compilation explicitly, so these emissions are redundant.
                // Note this must come after the DynamicData operators (so their
                // internal state stays consistent) and before ObserveOn (so the check
                // runs at emission time, not at deferred delivery time).
                .Where(_ => !IsBulkUpdating)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(changeSet =>
                {
                    CascadeDiagnostics.RecordMarker($@"UncompiledSub fired: Replaced={changeSet.Replaced} Adds={changeSet.Adds} IsBusy={IsBusy} IsBulkUpdating={IsBulkUpdating} AutoCompile={AutoCompile} anyUncompiled={RawActivities.Any(a => !a.IsCompiled)}");
                    if ((changeSet.Replaced + changeSet.Adds + changeSet.Refreshes) > 0)
                    {
                        lock (m_Lock)
                        {
                            // The changeset's uncompiled verdict is baked in at
                            // emission time and may be stale by the time it is
                            // delivered here (e.g. activities added during a load
                            // are emitted as uncompiled, but the load has already
                            // compiled them by the time the load releases m_Lock),
                            // so re-check before arming a redundant compile, which
                            // would mark the project scenario as updated.
                            //
                            // The check is the revision, not the flags of the
                            // activities. A compilation that was running when the
                            // edit was made marks every activity compiled when it
                            // publishes, the edited one among them, and the flag
                            // would then hide the edit. IsBusy is no help either:
                            // it is set for the whole of a compilation, and a
                            // compilation holds no lock.
                            if (m_Scheduler.HasUnabsorbedInputs())
                            {
                                if (AutoCompile)
                                {
                                    IsReadyToReviseTrackers = ReadyToRevise.Yes;
                                    IsReadyToCompile = ReadyToCompile.Yes;
                                    CascadeDiagnostics.RecordMarker(@"UncompiledSub armed IsReadyToCompile=Yes");
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

            // Compile off the arming thread: the settings-apply subscriptions
            // arm IsReadyToCompile on the UI thread, and the compile must
            // never run there. The selector revisions still complete first,
            // because they run inline during the IsReadyToReviseTrackers
            // raise, which every arm site performs before this one.
            m_CompileOnSettingsUpdateSub = this
                .WhenAnyValue(core => core.IsReadyToCompile)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(_ =>
                {
                    CascadeDiagnostics.RecordMarker($@"CompileOnSettingsUpdateSub fired: IsReadyToCompile={IsReadyToCompile} IsBusy={IsBusy}");
                    try
                    {
                        // Re-read rather than trust the delivered value (rule 7), and
                        // read the revision, not the property: whether the plan is due
                        // a compilation does not depend on a flag that a compilation
                        // in progress may be about to clear. Every compile takes the
                        // changes made before its snapshot into account, so two edits
                        // in quick succession arm this twice and the second delivery,
                        // which arrives after the first compile has absorbed both,
                        // finds nothing to do - and a redundant compile marks the
                        // project scenario as updated, exactly as the
                        // uncompiled-activities handler above explains.
                        DrainAutoCompiles();
                    }
                    catch (GraphCompilationTimeoutException)
                    {
                        // Swallowed deliberately. RunCompile has already logged the
                        // timeout and applied AbandonCompilation, and letting this
                        // escape would tear down this subscription along with every
                        // later compile it would have started. There is no dialog
                        // service here to report it either - see the note on
                        // AbandonCompilation.
                    }
                });

            // One compile, one cascade: a GraphCompilation change runs the whole
            // Build* cascade in dependency order (the same RunBuildCascade the
            // bulk update methods invoke actively inside their muted window) and
            // then bumps CompilationOutputRevision as the settled signal for
            // subscribers that need every output in place. Emissions raised
            // during a bulk update are dropped (rather than conflated): the drop
            // check must run at emission time (i.e. before ObserveOn), otherwise
            // the deferred taskpool invocation would run after the bulk update
            // window has already closed.
            m_BuildCascadeSub = this
                .WhenAnyValue(core => core.GraphCompilation)
                .Where(_ => !IsBulkUpdating)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(_ =>
                {
                    try
                    {
                        RunBuildCascade();
                    }
                    catch (GraphCompilationTimeoutException)
                    {
                        // Swallowed for the same reason as the compile subscription
                        // above: RunBuildCascade has already logged and applied the
                        // state, and an escaping exception would end the cascade
                        // subscription for the rest of the session.
                    }
                });

            // Risk metrics are the one output with a non-compile trigger: the
            // activity-severity settings feed them directly, so a settings
            // change must rebuild them without waiting for a compile.
            m_BuildRiskMetricsSub = this
                .WhenAnyValue(core => core.GraphSettings)
                .Where(_ => !IsBulkUpdating)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(_ => BuildRiskMetrics());

            // The activities made before the start start with it; those made after it start as they are made.
            m_PlanGraph.StartActivities();
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_SubscriptionLifetime.Kill();
            m_PlanGraph.KillBinds();
            m_AreActivitiesUncompiledSub?.Dispose();
            m_CompileOnSettingsUpdateSub?.Dispose();
            m_BuildCascadeSub?.Dispose();
            m_BuildRiskMetricsSub?.Dispose();
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
                m_ProjectFinishDays?.Dispose();
                m_ProjectFinishDate?.Dispose();
                m_HasActivities?.Dispose();
                m_HasResources?.Dispose();
                m_HasWorkStreams?.Dispose();
                m_HasPhases?.Dispose();
                ClearManagedActivities();
                m_PlanGraph?.Dispose();
                m_DisplaySettingsViewModel?.Dispose();
                m_DataGridScrollManager?.Dispose();
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
