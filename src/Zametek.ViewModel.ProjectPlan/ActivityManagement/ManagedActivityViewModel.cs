using ReactiveUI;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedActivityViewModel
        : DataErrorViewModelBase, IManagedActivityViewModel, IEditableObject
    {
        #region Fields

        private readonly ICoreViewModel m_CoreViewModel;
        private DateTimeOffset? m_MinimumEarliestStartDateTime;
        private DateTimeOffset? m_MaximumLatestFinishDateTime;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly VertexGraphCompiler m_VertexGraphCompiler;

        /// <summary>
        /// The leaf lock that makes writes to <see cref="DependentActivity"/> exclusive
        /// with the snapshot and publish steps of a compilation. Shared with every other
        /// activity and owned by the core view model, so one acquisition covers a whole
        /// snapshot or publish pass.
        /// </summary>
        /// <remarks>
        /// The rules that keep this lock safe (ARCHITECTURE section 7 rule 11): it is
        /// only ever held around writes to the activity's own state, never while raising
        /// a change notification, never while calling anything outside this class, and
        /// never while taking or holding another lock. Nothing it guards is reachable
        /// from a property getter, so it cannot appear in a lock cycle.
        /// </remarks>
        private readonly Lock m_DataLock;

        private readonly IDisposable? m_ProjectStartSub;
        private readonly IDisposable? m_DateTimeCalculatorCalculatorModeSub;
        private readonly IDisposable? m_DateTimeCalculatorDisplayModeSub;
        private readonly IDisposable? m_CompilationSub;

        #endregion

        #region Ctors

        public ManagedActivityViewModel(
            ICoreViewModel coreViewModel,
            IDependentActivity dependentActivity,
            IDateTimeCalculator dateTimeCalculator,
            VertexGraphCompiler vertexGraphCompiler,
            Lock dataLock,
            DateTimeOffset projectStart,
            IEnumerable<ActivityTrackerModel>? trackers,
            DateTimeOffset? minimumEarliestStartDateTime,
            DateTimeOffset? maximumLatestFinishDateTime)
            : base()
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(dependentActivity);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(vertexGraphCompiler);

            // Checked directly rather than through ArgumentNullException.ThrowIfNull,
            // which takes an object and so would convert the Lock (CS9216).
            if (dataLock is null)
            {
                throw new ArgumentNullException(nameof(dataLock));
            }

            m_CoreViewModel = coreViewModel;
            DependentActivity = dependentActivity;
            m_DateTimeCalculator = dateTimeCalculator;
            m_ProjectStart = projectStart;
            m_IsEditMuted = false;
            m_MinimumEarliestStartDateTime = minimumEarliestStartDateTime;
            m_MaximumLatestFinishDateTime = maximumLatestFinishDateTime;
            m_VertexGraphCompiler = vertexGraphCompiler;
            m_DataLock = dataLock;

            ResourceSelector = new ResourceSelectorViewModel();
            m_ResourceSettings = m_CoreViewModel.ResourceSettings;
            RefreshResourceSelector();

            WorkStreamSelector = new WorkStreamSelectorViewModel();
            m_WorkStreamSettings = m_CoreViewModel.WorkStreamSettings;
            RefreshWorkStreamSelector();

            // Write the seeded selections straight back into the underlying activity.
            // The selectors only keep target ids that exist in the current settings, so
            // this prunes ids referring to since-removed resources or work streams the
            // moment the activity is built, keeping the activity's target sets and its
            // selectors identical from birth. This write-back used to happen through
            // the deferred settings subscriptions (removed - see below), which left a
            // window where the compiler saw the unpruned sets.
            UpdateActivityTargetResources();
            UpdateActivityTargetWorkStreams();

            if (MinimumEarliestStartDateTime.HasValue)
            {
                SetMinimumEarliestStartTimes(MinimumEarliestStartDateTime, skipValidation: true);
            }
            else if (MinimumEarliestStartTime.HasValue)
            {
                SetMinimumEarliestStartTimes(MinimumEarliestStartTime);
            }

            if (MaximumLatestFinishDateTime.HasValue)
            {
                SetMaximumLatestFinishTimes(MaximumLatestFinishDateTime, skipValidation: true);
            }
            else if (MaximumLatestFinishTime.HasValue)
            {
                SetMaximumLatestFinishTimes(MaximumLatestFinishTime);
            }

            TrackerSet = new ActivityTrackerSetViewModel(
                m_CoreViewModel, DependentActivity.Id, trackers ?? []);

            // The subscriptions below observe on Scheduler.CurrentThread so that their
            // callbacks run inline on whichever thread raises the change, and are
            // therefore complete before the CoreViewModel can proceed to compilation.
            //
            // Resource and work stream settings are deliberately NOT observed here:
            // the CoreViewModel pushes them in synchronously, under its own lock, via
            // SetResourceSettings/SetWorkStreamSettings. They used to arrive through
            // subscriptions deferred to the UI thread, which could clear and rebuild
            // this activity's live target sets while a compile on another thread was
            // cloning them - the torn-HashSet corruption diagnosed from the
            // zametek-deadlock-2 dump. Nothing that mutates the underlying activity
            // may be deferred like that; it must run synchronously wherever the
            // change is made, so it is ordered against the compile by m_Lock.

            m_ShowDates = this
                .WhenAnyValue(x => x.m_CoreViewModel.DisplaySettingsViewModel.ShowDates)
                .ToProperty(this, x => x.ShowDates);

            m_HasResources = this
                .WhenAnyValue(x => x.m_CoreViewModel.HasResources)
                .ToProperty(this, x => x.HasResources);

            m_HasWorkStreams = this
                .WhenAnyValue(x => x.m_CoreViewModel.HasWorkStreams)
                .ToProperty(this, x => x.HasWorkStreams);

            m_ProjectStartSub = this
                .WhenAnyValue(x => x.m_CoreViewModel.ProjectStart)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(x => ProjectStart = x);

            m_DateTimeCalculatorCalculatorModeSub = this
                .WhenAnyValue(
                    x => x.m_DateTimeCalculator.NonWorkingDayMode,
                    x => x.m_CoreViewModel.HolidaySettings)
                //.ObserveOn(RxSchedulers.TaskpoolScheduler)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(_ => UpdateEarliestStartAndLatestFinishDateTimes());

            m_DateTimeCalculatorDisplayModeSub = this
                .WhenAnyValue(x => x.m_DateTimeCalculator.DisplayMode)
                //.ObserveOn(RxSchedulers.TaskpoolScheduler)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(_ => RefreshStartAndFinishValues());

            // Skip the initial value emitted at subscription, otherwise a newly
            // created activity would be marked as compiled without a compilation
            // having taken place. Stay on the current thread so the activity is
            // marked as compiled synchronously when a compilation is published;
            // deferring this (e.g. to the taskpool) leaves a window after a load
            // completes where the activity still reads as uncompiled, which
            // randomly arms a redundant auto-compile that marks the project
            // scenario as updated.
            m_CompilationSub = this
                .WhenAnyValue(x => x.m_CoreViewModel.GraphCompilation)
                .Skip(1)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(_ => SetAsCompiled());

            m_IsCompiled = false;
        }

        #endregion

        #region Properties

        private ResourceSettingsModel m_ResourceSettings;

        private WorkStreamSettingsModel m_WorkStreamSettings;

        public IDependentActivity DependentActivity { get; }

        #endregion

        #region Private Methods

        private void SetMinimumEarliestStartTimes(int? input)
        {
            (int? intValue, DateTimeOffset? dateTimeOffsetValue) = m_DateTimeCalculator.CalculateTimeAndDateTime(ProjectStart, input);

            // Validate integer value.
            ValidateMinimumEarliestStartTime(intValue);

            // Set integer and DateTimeOffset values.
            lock (m_DataLock)
            {
                DependentActivity.MinimumEarliestStartTime = intValue;
            }
            this.RaisePropertyChanged(nameof(MinimumEarliestStartTime));
            this.RaiseAndSetIfChanged(ref m_MinimumEarliestStartDateTime, dateTimeOffsetValue, nameof(MinimumEarliestStartDateTime));
            RefreshStartAndFinishValues();
        }

        private void SetMinimumEarliestStartTimes(DateTimeOffset? input, bool skipValidation = false)
        {
            (int? intValue, DateTimeOffset? dateTimeOffsetValue) = m_DateTimeCalculator.CalculateTimeAndDateTime(ProjectStart, input);

            if (!skipValidation)
            {
                // Validate integer value.
                ValidateMinimumEarliestStartTime(intValue);
            }

            // Set integer and DateTimeOffset values.
            lock (m_DataLock)
            {
                DependentActivity.MinimumEarliestStartTime = intValue;
            }
            this.RaisePropertyChanged(nameof(MinimumEarliestStartTime));
            this.RaiseAndSetIfChanged(ref m_MinimumEarliestStartDateTime, dateTimeOffsetValue, nameof(MinimumEarliestStartDateTime));
            RefreshStartAndFinishValues();
        }

        private void SetMaximumLatestFinishTimes(int? input)
        {
            (int? intValue, DateTimeOffset? dateTimeOffsetValue) = m_DateTimeCalculator.CalculateTimeAndDateTime(ProjectStart, input);

            // Validate integer value.
            ValidateMaximumLatestFinishTime(intValue);

            // Set integer and DateTimeOffset values.
            lock (m_DataLock)
            {
                DependentActivity.MaximumLatestFinishTime = intValue;
            }
            this.RaisePropertyChanged(nameof(MaximumLatestFinishTime));
            this.RaiseAndSetIfChanged(ref m_MaximumLatestFinishDateTime, dateTimeOffsetValue, nameof(MaximumLatestFinishDateTime));
            RefreshStartAndFinishValues();
        }

        private void SetMaximumLatestFinishTimes(DateTimeOffset? input, bool skipValidation = false)
        {
            (int? intValue, DateTimeOffset? dateTimeOffsetValue) = m_DateTimeCalculator.CalculateTimeAndDateTime(ProjectStart, input);

            if (!skipValidation)
            {
                // Validate integer value.
                ValidateMaximumLatestFinishTime(intValue);
            }

            // Set integer and DateTimeOffset values.
            lock (m_DataLock)
            {
                DependentActivity.MaximumLatestFinishTime = intValue;
            }
            this.RaisePropertyChanged(nameof(MaximumLatestFinishTime));
            this.RaiseAndSetIfChanged(ref m_MaximumLatestFinishDateTime, dateTimeOffsetValue, nameof(MaximumLatestFinishDateTime));
            RefreshStartAndFinishValues();
        }

        private void ValidateDuration(int input)
        {
            Validate(MinimumFreeSlack, MinimumEarliestStartTime, MaximumLatestFinishTime, input);
        }

        private void ValidateMinimumFreeSlack(int? input)
        {
            Validate(input, MinimumEarliestStartTime, MaximumLatestFinishTime, Duration);
        }

        private void ValidateMinimumEarliestStartTime(int? input)
        {
            Validate(MinimumFreeSlack, input, MaximumLatestFinishTime, Duration);
        }

        private void ValidateMaximumLatestFinishTime(int? input)
        {
            Validate(MinimumFreeSlack, MinimumEarliestStartTime, input, Duration);
        }

        private void Validate(
            int? minimumFreeSlack,
            int? minimumEarliestStartTime,
            int? maximumLatestFinishTime,
            int duration)
        {
            ClearErrors(nameof(Duration));
            ClearErrors(nameof(MinimumFreeSlack));
            ClearErrors(nameof(MinimumEarliestStartTime));
            ClearErrors(nameof(MinimumEarliestStartDateTime));
            ClearErrors(nameof(MaximumLatestFinishTime));
            ClearErrors(nameof(MaximumLatestFinishDateTime));

            {
                string? errorMessage = ConstraintsValidationRule.ValidateDuration(minimumEarliestStartTime, maximumLatestFinishTime, duration);
                if (errorMessage is not null)
                {
                    SetError(nameof(Duration), errorMessage);
                    SetError(nameof(MinimumEarliestStartTime), errorMessage);
                    SetError(nameof(MinimumEarliestStartDateTime), errorMessage);
                    SetError(nameof(MaximumLatestFinishTime), errorMessage);
                    SetError(nameof(MaximumLatestFinishDateTime), errorMessage);
                }
            }

            {
                string? errorMessage = ConstraintsValidationRule.ValidateMinimumFreeSlack(minimumFreeSlack, minimumEarliestStartTime, maximumLatestFinishTime);
                if (errorMessage is not null)
                {
                    SetError(nameof(MinimumFreeSlack), errorMessage);
                    SetError(nameof(MinimumEarliestStartTime), errorMessage);
                    SetError(nameof(MinimumEarliestStartDateTime), errorMessage);
                    SetError(nameof(MaximumLatestFinishTime), errorMessage);
                    SetError(nameof(MaximumLatestFinishDateTime), errorMessage);
                }
            }
        }

        private void UpdateActivityTargetResources()
        {
            // The set is cleared and refilled, so a compilation snapshotting it at the
            // wrong moment would clone a half-built set; the lock is what stops that.
            lock (m_DataLock)
            {
                DependentActivity.TargetResources.Clear();
                DependentActivity.TargetResources.UnionWith(ResourceSelector.SelectedResourceIds);
            }
            this.RaisePropertyChanged(nameof(TargetResources));
            this.RaisePropertyChanged(nameof(ResourceSelector));
            this.RaisePropertyChanged(nameof(AllocatedToResourcesString));
        }

        private void SetNewTargetResources()
        {
            UpdateActivityTargetResources();
            RefreshResourceSelector();
            UpdateActivityTargetResources();
        }

        private void RefreshResourceSelector()
        {
            var selectedTargetResources = new HashSet<int>(DependentActivity.TargetResources);

            IEnumerable<TargetResourceModel> targetResources = m_ResourceSettings
                .Resources.Select(
                    x => new TargetResourceModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                    });

            ResourceSelector.SetTargetResources(targetResources, selectedTargetResources);
        }

        private void UpdateActivityTargetWorkStreams()
        {
            lock (m_DataLock)
            {
                DependentActivity.TargetWorkStreams.Clear();
                DependentActivity.TargetWorkStreams.UnionWith(WorkStreamSelector.SelectedWorkStreamIds);
            }
            this.RaisePropertyChanged(nameof(TargetWorkStreams));
            this.RaisePropertyChanged(nameof(WorkStreamSelector));
        }

        private void SetNewTargetWorkStreams()
        {
            UpdateActivityTargetWorkStreams();
            RefreshWorkStreamSelector();
            UpdateActivityTargetWorkStreams();
        }

        private void RefreshWorkStreamSelector()
        {
            var selectedTargetWorkStreams = new HashSet<int>(DependentActivity.TargetWorkStreams);

            IEnumerable<TargetWorkStreamModel> targetWorkStreams = m_WorkStreamSettings
                .WorkStreams.Select(
                    x => new TargetWorkStreamModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                        IsPhase = x.IsPhase,
                    });

            WorkStreamSelector.SetTargetWorkStreams(targetWorkStreams, selectedTargetWorkStreams);
        }

        private void UpdateEarliestStartAndLatestFinishDateTimes()
        {
            RefreshStartAndFinishValues();
            SetMinimumEarliestStartTimes(m_MinimumEarliestStartDateTime, skipValidation: true);
            SetMaximumLatestFinishTimes(m_MaximumLatestFinishDateTime, skipValidation: true);
        }

        private void RefreshStartAndFinishValues()
        {
            this.RaisePropertyChanged(nameof(EarliestStartTime));
            this.RaisePropertyChanged(nameof(LatestStartTime));
            this.RaisePropertyChanged(nameof(EarliestFinishTime));
            this.RaisePropertyChanged(nameof(LatestFinishTime));
            this.RaisePropertyChanged(nameof(EarliestStartDateTimeOffset));
            this.RaisePropertyChanged(nameof(LatestStartDateTimeOffset));
            this.RaisePropertyChanged(nameof(EarliestFinishDateTimeOffset));
            this.RaisePropertyChanged(nameof(LatestFinishDateTimeOffset));
            this.RaisePropertyChanged(nameof(MinimumEarliestStartTime));
            this.RaisePropertyChanged(nameof(MinimumEarliestStartDateTime));
            this.RaisePropertyChanged(nameof(MaximumLatestFinishTime));
            this.RaisePropertyChanged(nameof(MaximumLatestFinishDateTime));
        }

        private void SetAsCompiled()
        {
            m_IsCompiled = true;
            this.RaisePropertyChanged(nameof(IsIsolated));
            this.RaisePropertyChanged(nameof(AllocatedToResourcesString));
        }

        #endregion

        #region IManagedActivityViewModel Members

        public int DisplayOrder
        {
            get => DependentActivity.DisplayOrder;
            set
            {
                DependentActivity.DisplayOrder = value;
                this.RaisePropertyChanged();
            }
        }

        public bool IsIsolated => m_VertexGraphCompiler.IsIsolated(Id);

        private bool m_IsCompiled;
        public bool IsCompiled
        {
            get => m_IsCompiled;
            private set
            {
                m_IsCompiled = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(AllocatedToResourcesString));
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowDates;
        public bool ShowDates => m_ShowDates.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasResources;
        public bool HasResources => m_HasResources.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasWorkStreams;
        public bool HasWorkStreams => m_HasWorkStreams.Value;

        private DateTimeOffset m_ProjectStart;
        public DateTimeOffset ProjectStart
        {
            get => m_ProjectStart;
            set
            {
                this.RaiseAndSetIfChanged(ref m_ProjectStart, value);
                RefreshStartAndFinishValues();
                //this.RaisePropertyChanged(nameof(EarliestStartDateTimeOffset));
                //this.RaisePropertyChanged(nameof(LatestStartDateTimeOffset));
                //this.RaisePropertyChanged(nameof(EarliestFinishDateTimeOffset));
                //this.RaisePropertyChanged(nameof(LatestFinishDateTimeOffset));
                SetMinimumEarliestStartTimes(m_MinimumEarliestStartDateTime);
                SetMaximumLatestFinishTimes(m_MaximumLatestFinishDateTime);
            }
        }

        public string DependenciesString
        {
            get => string.Join(DependenciesStringValidationRule.Separator, Dependencies.OrderBy(x => x));
            set
            {
                //ClearErrors();
                (IEnumerable<int>? updatedDependencies, string? _) = DependenciesStringValidationRule.Validate(value, Id);
                //if (errorMessage is not null)
                //{
                //    SetError(nameof(DependenciesString), errorMessage);
                //}

                if (updatedDependencies is not null)
                {
                    // This rewrites the activity's own dependency sets as well as the
                    // graph edges, so it is a write to the data a compilation snapshots.
                    lock (m_DataLock)
                    {
                        m_VertexGraphCompiler.SetActivityDependencies(Id, [.. updatedDependencies], PlanningDependencies);
                    }
                }
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(Dependencies));
            }
        }

        public string PlanningDependenciesString
        {
            get => string.Join(DependenciesStringValidationRule.Separator, PlanningDependencies.OrderBy(x => x));
            set
            {
                //ClearErrors();
                (IEnumerable<int>? updatedPlanningDependencies, string? _) = DependenciesStringValidationRule.Validate(value, Id);
                //if (errorMessage is not null)
                //{
                //    SetError(nameof(DependenciesString), errorMessage);
                //}

                if (updatedPlanningDependencies is not null)
                {
                    lock (m_DataLock)
                    {
                        m_VertexGraphCompiler.SetActivityDependencies(Id, Dependencies, [.. updatedPlanningDependencies]);
                    }
                }
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(PlanningDependencies));
            }
        }

        public string ResourceDependenciesString => string.Join(DependenciesStringValidationRule.Separator, ResourceDependencies.OrderBy(x => x));

        public string SuccessorsString => string.Join(DependenciesStringValidationRule.Separator, Successors.OrderBy(x => x));

        public int Id => DependentActivity.Id;

        public bool CanBeRemoved => DependentActivity.CanBeRemoved;

        public string? Name
        {
            get => DependentActivity.Name;
            set
            {
                lock (m_DataLock)
                {
                    DependentActivity.Name = value;
                }
                this.RaisePropertyChanged();
            }
        }

        public string? Notes
        {
            get => DependentActivity.Notes;
            set
            {
                lock (m_DataLock)
                {
                    DependentActivity.Notes = value;
                }
                this.RaisePropertyChanged();
            }
        }

        public HashSet<int> TargetWorkStreams => DependentActivity.TargetWorkStreams;

        public HashSet<int> TargetResources => DependentActivity.TargetResources;

        public LogicalOperator TargetResourceOperator
        {
            get => DependentActivity.TargetResourceOperator;
            set
            {
                lock (m_DataLock)
                {
                    DependentActivity.TargetResourceOperator = value;
                }
                this.RaisePropertyChanged();
            }
        }

        public HashSet<int> AllocatedToResources => DependentActivity.AllocatedToResources;

        public string AllocatedToResourcesString
        {
            get
            {
                HashSet<int> allocatedToResources = AllocatedToResources;

                if (!m_CoreViewModel.HasResources)
                {
                    return string.Join(
                        DependenciesStringValidationRule.Separator,
                        allocatedToResources.Order());
                }

                return ResourceSelector.GetAllocatedToResourcesString(allocatedToResources);
            }
        }

        public bool IsDummy => DependentActivity.IsDummy;

        public bool HasNoCost
        {
            get => DependentActivity.HasNoCost;
            set
            {
                if (DependentActivity.HasNoCost != value)
                {
                    BeginEdit();
                    lock (m_DataLock)
                    {
                        DependentActivity.HasNoCost = value;
                    }
                    EndEdit();
                }
                this.RaisePropertyChanged();
            }
        }

        public bool HasNoBilling
        {
            get => DependentActivity.HasNoBilling;
            set
            {
                if (DependentActivity.HasNoBilling != value)
                {
                    BeginEdit();
                    lock (m_DataLock)
                    {
                        DependentActivity.HasNoBilling = value;
                    }
                    EndEdit();
                }
                this.RaisePropertyChanged();
            }
        }

        public bool HasNoEffort
        {
            get => DependentActivity.HasNoEffort;
            set
            {
                if (DependentActivity.HasNoEffort != value)
                {
                    BeginEdit();
                    lock (m_DataLock)
                    {
                        DependentActivity.HasNoEffort = value;
                    }
                    EndEdit();
                }
                this.RaisePropertyChanged();
            }
        }

        public bool HasNoRisk
        {
            get => DependentActivity.HasNoRisk;
            set
            {
                if (DependentActivity.HasNoRisk != value)
                {
                    BeginEdit();
                    lock (m_DataLock)
                    {
                        DependentActivity.HasNoRisk = value;
                    }
                    EndEdit();
                }
                this.RaisePropertyChanged();
            }
        }

        public int Duration
        {
            get => DependentActivity.Duration;
            set
            {
                if (value < 0)
                {
                    value = 0;
                }

                ValidateDuration(value);

                lock (m_DataLock)
                {
                    DependentActivity.Duration = value;
                }
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(IsDummy));
                this.RaisePropertyChanged(nameof(IsCritical));
                this.RaisePropertyChanged(nameof(EarliestFinishTime));
                this.RaisePropertyChanged(nameof(EarliestFinishDateTimeOffset));
                this.RaisePropertyChanged(nameof(LatestStartTime));
                this.RaisePropertyChanged(nameof(LatestStartDateTimeOffset));
                this.RaisePropertyChanged(nameof(TotalSlack));
                this.RaisePropertyChanged(nameof(InterferingSlack));
            }
        }

        public int? TotalSlack => DependentActivity.TotalSlack;

        public int? FreeSlack
        {
            get => DependentActivity.FreeSlack;
            set
            {
                lock (m_DataLock)
                {
                    DependentActivity.FreeSlack = value;
                }
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(InterferingSlack));
                this.RaisePropertyChanged(nameof(DependenciesString));
                this.RaisePropertyChanged(nameof(PlanningDependenciesString));
                this.RaisePropertyChanged(nameof(ResourceDependenciesString));
                this.RaisePropertyChanged(nameof(SuccessorsString));
            }
        }

        public int? InterferingSlack => DependentActivity.InterferingSlack;

        public bool IsCritical => DependentActivity.IsCritical;

        public int? EarliestStartTime
        {
            get => DependentActivity.EarliestStartTime;
            set
            {
                lock (m_DataLock)
                {
                    DependentActivity.EarliestStartTime = value;
                }
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(EarliestStartDateTimeOffset));
                this.RaisePropertyChanged(nameof(EarliestFinishTime));
                this.RaisePropertyChanged(nameof(EarliestFinishDateTimeOffset));
                this.RaisePropertyChanged(nameof(TotalSlack));
                this.RaisePropertyChanged(nameof(IsCritical));
                this.RaisePropertyChanged(nameof(InterferingSlack));
                this.RaisePropertyChanged(nameof(DependenciesString));
                this.RaisePropertyChanged(nameof(PlanningDependenciesString));
                this.RaisePropertyChanged(nameof(ResourceDependenciesString));
                this.RaisePropertyChanged(nameof(SuccessorsString));
            }
        }

        public DateTimeOffset? EarliestStartDateTimeOffset
        {
            get
            {
                if (EarliestStartTime.HasValue)
                {
                    if (MinimumEarliestStartDateTime.HasValue)
                    {
                        return m_DateTimeCalculator.DisplayEarliestStartDate(
                            MinimumEarliestStartDateTime.GetValueOrDefault(),
                            m_DateTimeCalculator.AddDays(
                                ProjectStart,
                                EarliestStartTime.GetValueOrDefault()),
                            Duration);
                    }

                    return m_DateTimeCalculator.DisplayEarliestStartDate(
                        ProjectStart,
                        m_DateTimeCalculator.AddDays(
                            ProjectStart,
                            EarliestStartTime.GetValueOrDefault()),
                        Duration);
                }
                return null;
            }
        }

        public int? LatestStartTime => DependentActivity.LatestStartTime;

        public DateTimeOffset? LatestStartDateTimeOffset
        {
            get
            {
                if (LatestStartTime.HasValue)
                {
                    return m_DateTimeCalculator.DisplayLatestStartDate(
                        EarliestStartDateTimeOffset.GetValueOrDefault(),
                        m_DateTimeCalculator.AddDays(
                            ProjectStart,
                            LatestStartTime.GetValueOrDefault()),
                        Duration);
                }
                return null;
            }
        }

        public int? EarliestFinishTime => DependentActivity.EarliestFinishTime;

        public DateTimeOffset? EarliestFinishDateTimeOffset
        {
            get
            {
                if (EarliestFinishTime.HasValue)
                {
                    return m_DateTimeCalculator.DisplayFinishDate(
                        EarliestStartDateTimeOffset.GetValueOrDefault(),
                        m_DateTimeCalculator.AddDays(
                            ProjectStart,
                            EarliestFinishTime.GetValueOrDefault()),
                        Duration);
                }
                return null;
            }
        }

        public int? LatestFinishTime
        {
            get => DependentActivity.LatestFinishTime;
            set
            {
                lock (m_DataLock)
                {
                    DependentActivity.LatestFinishTime = value;
                }
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(LatestFinishDateTimeOffset));
                this.RaisePropertyChanged(nameof(LatestStartTime));
                this.RaisePropertyChanged(nameof(LatestStartDateTimeOffset));
                this.RaisePropertyChanged(nameof(TotalSlack));
                this.RaisePropertyChanged(nameof(IsCritical));
                this.RaisePropertyChanged(nameof(InterferingSlack));
                this.RaisePropertyChanged(nameof(DependenciesString));
                this.RaisePropertyChanged(nameof(PlanningDependenciesString));
                this.RaisePropertyChanged(nameof(ResourceDependenciesString));
                this.RaisePropertyChanged(nameof(SuccessorsString));
            }
        }

        public DateTimeOffset? LatestFinishDateTimeOffset
        {
            get
            {
                if (LatestFinishTime.HasValue)
                {
                    return m_DateTimeCalculator.DisplayFinishDate(
                        LatestStartDateTimeOffset.GetValueOrDefault(),
                        m_DateTimeCalculator.AddDays(
                            ProjectStart,
                            LatestFinishTime.GetValueOrDefault()),
                        Duration);
                }
                return null;
            }
        }

        public int? MinimumFreeSlack
        {
            get => DependentActivity.MinimumFreeSlack;
            set
            {
                if (value.HasValue && value < 0)
                {
                    value = 0;
                }

                ValidateMinimumFreeSlack(value);

                lock (m_DataLock)
                {
                    DependentActivity.MinimumFreeSlack = value;
                }
                this.RaisePropertyChanged();
            }
        }

        public int? MinimumEarliestStartTime
        {
            get => DependentActivity.MinimumEarliestStartTime;
            set => SetMinimumEarliestStartTimes(value);
        }

        public DateTime? MinimumEarliestStartDateTime
        {
            get => m_MinimumEarliestStartDateTime?.DateTime;
            set
            {
                // Convert to local now using TimeProvider as we do not know
                // if the input is provided as just a datetime from XAML.
                DateTimeOffset? input = value is null ? null : m_DateTimeCalculator.GetLocal(value.Value);
                SetMinimumEarliestStartTimes(input);
            }
        }

        public int? MaximumLatestFinishTime
        {
            get => DependentActivity.MaximumLatestFinishTime;
            set => SetMaximumLatestFinishTimes(value);
        }

        public DateTime? MaximumLatestFinishDateTime
        {
            get
            {
                if (m_MaximumLatestFinishDateTime.HasValue)
                {
                    return m_DateTimeCalculator.MaximumLatestFinishDateOut(
                        EarliestStartDateTimeOffset.GetValueOrDefault(),
                        m_MaximumLatestFinishDateTime.GetValueOrDefault(),
                        Duration).DateTime;
                }

                return null;
            }
            set
            {
                // Convert to local now using TimeProvider as we do not know
                // if the input is provided as just a datetime from XAML.
                DateTimeOffset? input = value is null ? null : m_DateTimeCalculator.GetLocal(value.Value);

                if (input.HasValue)
                {
                    input = m_DateTimeCalculator.MaximumLatestFinishDateIn(
                        EarliestStartDateTimeOffset.GetValueOrDefault(),
                        input.GetValueOrDefault(),
                        Duration);
                }

                SetMaximumLatestFinishTimes(input);
            }
        }

        public IResourceSelectorViewModel ResourceSelector { get; }

        public IWorkStreamSelectorViewModel WorkStreamSelector { get; }

        public bool OverrideColor
        {
            get => DependentActivity.OverrideColor;
            set
            {
                if (DependentActivity.OverrideColor != value)
                {
                    BeginEdit();
                    lock (m_DataLock)
                    {
                        DependentActivity.OverrideColor = value;
                    }
                    EndEdit();
                }
                this.RaisePropertyChanged();
            }
        }

        public ColorFormatModel ColorFormat
        {
            get => DependentActivity.ColorFormat;
            set
            {
                if (DependentActivity.ColorFormat != value)
                {
                    BeginEdit();
                    lock (m_DataLock)
                    {
                        DependentActivity.ColorFormat = value;
                    }
                    EndEdit();
                }
                this.RaisePropertyChanged();
            }
        }

        public IActivityTrackerSetViewModel TrackerSet { get; }

        public List<ActivityTrackerModel> Trackers => TrackerSet.Trackers;

        public HashSet<int> Dependencies => DependentActivity.Dependencies;

        public HashSet<int> PlanningDependencies => DependentActivity.PlanningDependencies;

        public HashSet<int> ResourceDependencies => DependentActivity.ResourceDependencies;

        public HashSet<int> Successors => DependentActivity.Successors;

        public void SetAsReadOnly()
        {
            lock (m_DataLock)
            {
                DependentActivity.SetAsReadOnly();
            }
        }

        public void SetAsRemovable()
        {
            lock (m_DataLock)
            {
                DependentActivity.SetAsRemovable();
            }
        }

        /// <summary>
        /// Absorbs new resource settings: stores them, rebuilds the resource selector,
        /// and reconciles the activity's target resources against the resources that
        /// now exist. The CoreViewModel invokes this synchronously, under its lock,
        /// whenever its resource settings change - always before a compile can start
        /// against the new settings. It must never be deferred to another thread: the
        /// target sets belong to the live activity that the compiler clones, and a
        /// deferred mutation can tear those clones mid-copy.
        /// </summary>
        public void SetResourceSettings(ResourceSettingsModel resourceSettings)
        {
            ArgumentNullException.ThrowIfNull(resourceSettings);
            m_ResourceSettings = resourceSettings;
            SetNewTargetResources();
        }

        /// <summary>
        /// The work stream counterpart to <see cref="SetResourceSettings"/>.
        /// </summary>
        public void SetWorkStreamSettings(WorkStreamSettingsModel workStreamSettings)
        {
            ArgumentNullException.ThrowIfNull(workStreamSettings);
            m_WorkStreamSettings = workStreamSettings;
            SetNewTargetWorkStreams();
        }

        public DependentActivityModel DeepCopy()
        {
            var activityModel = new ActivityModel
            {
                Id = Id,
                DisplayOrder = DisplayOrder,
                Name = Name ?? string.Empty,
                TargetWorkStreams = [.. TargetWorkStreams],
                TargetResources = [.. TargetResources],
                TargetResourceOperator = TargetResourceOperator,
                AllocatedToResources = [.. AllocatedToResources],
                CanBeRemoved = CanBeRemoved,
                HasNoCost = HasNoCost,
                HasNoBilling = HasNoBilling,
                HasNoEffort = HasNoEffort,
                HasNoRisk = HasNoRisk,
                Duration = Duration,
                FreeSlack = FreeSlack,
                TotalSlack = TotalSlack,
                EarliestStartTime = EarliestStartTime,
                LatestStartTime = LatestStartTime,
                EarliestFinishTime = EarliestFinishTime,
                LatestFinishTime = LatestFinishTime,
                MinimumFreeSlack = MinimumFreeSlack,
                MinimumEarliestStartTime = MinimumEarliestStartTime,
                MinimumEarliestStartDateTime = MinimumEarliestStartDateTime,
                MaximumLatestFinishTime = MaximumLatestFinishTime,
                MaximumLatestFinishDateTime = MaximumLatestFinishDateTime,
                OverrideColor = OverrideColor,
                ColorFormat = ColorFormat,
                Notes = Notes ?? string.Empty,
                Trackers = TrackerSet.CloneTrackers(),
            };

            return new DependentActivityModel
            {
                Activity = activityModel,
                Dependencies = [.. Dependencies],
                PlanningDependencies = [.. PlanningDependencies],
                ResourceDependencies = [.. ResourceDependencies],
                Successors = [.. Successors],
            };
        }

        /// <summary>
        /// Takes an independent copy of the underlying activity. This is the input half
        /// of a compilation: the compiler is given these copies rather than the live
        /// activities, so nothing it does can be disturbed by an edit made while it runs,
        /// and no edit can observe the half-written state it would otherwise leave behind.
        /// <see cref="SetCompiledValues"/> is the output half.
        /// </summary>
        /// <remarks>
        /// The trackers are read from the tracker set rather than the underlying activity,
        /// because the tracker set is where they are actually maintained. That read is
        /// outside the lock, as the tracker set guards its own state; trackers are
        /// immutable records, so the copy is coherent whatever else is happening.
        /// </remarks>
        public object CloneObject()
        {
            IDependentActivity activity;

            lock (m_DataLock)
            {
                activity = (IDependentActivity)DependentActivity.CloneObject();
            }

            activity.Trackers.Clear();
            activity.Trackers.AddRange(Trackers);
            return activity;
        }

        /// <summary>
        /// Absorbs the results of a compilation from the compiled copy of this activity.
        /// The output half of the pair described on <see cref="CloneObject"/>.
        /// </summary>
        /// <remarks>
        /// Every value a compilation produces is applied here, and nothing else is: the
        /// times and slack it calculates, the resources it allocated the activity to, the
        /// dependencies that allocation implies, and the successors it derived. The
        /// activity's own inputs - duration, targets, constraints, the dependencies the
        /// user set - are not touched, because a compilation never changes them, and
        /// writing them back would overwrite any edit made while it ran.
        /// <para>
        /// The times go through this view model's own setters rather than straight into
        /// the underlying activity, because those setters are how a compilation's results
        /// have always reached the view: the compiler held the view models themselves as
        /// its graph, so calculating a time called the setter, which announced it along
        /// with everything derived from it. Compiling a copy of the plan must not lose
        /// that, or the grid keeps showing the values from the compilation before.
        /// </para>
        /// <para>
        /// The collections are written first, because the setters that follow announce the
        /// properties derived from them. They are the only part written directly, as they
        /// have no setter of their own; the announcements the times make cover them.
        /// </para>
        /// <para>
        /// None of this arms another compilation. That happens only when an activity is
        /// marked uncompiled, which is a consequence of an edit being committed
        /// (IEditableObject.EndEdit), never of a value being announced.
        /// </para>
        /// </remarks>
        public void SetCompiledValues(IDependentActivity compiledActivity)
        {
            ArgumentNullException.ThrowIfNull(compiledActivity);

            if (compiledActivity.Id != Id)
            {
                throw new ArgumentException(
                    $@"The compiled activity must be the compiled copy of this activity, but its ID is {compiledActivity.Id} and this activity's ID is {Id}.",
                    nameof(compiledActivity));
            }

            lock (m_DataLock)
            {
                DependentActivity.AllocatedToResources.Clear();
                DependentActivity.AllocatedToResources.UnionWith(compiledActivity.AllocatedToResources);

                DependentActivity.ResourceDependencies.Clear();
                DependentActivity.ResourceDependencies.UnionWith(compiledActivity.ResourceDependencies);

                DependentActivity.Successors.Clear();
                DependentActivity.Successors.UnionWith(compiledActivity.Successors);
            }

            // Outside the lock: these announce, and nothing may be announced while the
            // lock is held (ARCHITECTURE section 7 rules 6 and 11).
            EarliestStartTime = compiledActivity.EarliestStartTime;
            LatestFinishTime = compiledActivity.LatestFinishTime;
            FreeSlack = compiledActivity.FreeSlack;
        }

        #endregion

        #region IEditableObject Members

        private bool m_IsDirty;

        public void BeginEdit()
        {
            // Bug Fix: Windows Controls call EndEdit twice; Once
            // from IEditableCollectionView, and once from BindingGroup.
            // This makes sure it only happens once after a BeginEdit.
            m_IsDirty = true;
        }

        public void EndEdit()
        {
            if (m_IsDirty)
            {
                m_IsDirty = false;
                UpdateActivityTargetResources();
                UpdateActivityTargetWorkStreams();
                TrackerSet.RefreshIndex();
                m_CoreViewModel.IsProjectScenarioUpdated = true;

                if (!IsEditMuted)
                {
                    IsCompiled = false;
                }
            }
        }

        public void CancelEdit()
        {
            m_IsDirty = false;
        }

        #endregion

        #region IMuteEdits Members

        private bool m_IsEditMuted;
        public bool IsEditMuted
        {
            get => m_IsEditMuted;
            set => this.RaiseAndSetIfChanged(ref m_IsEditMuted, value);
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_ProjectStartSub?.Dispose();
            m_DateTimeCalculatorCalculatorModeSub?.Dispose();
            m_DateTimeCalculatorDisplayModeSub?.Dispose();
            m_CompilationSub?.Dispose();
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
                TrackerSet.Dispose();
                m_ShowDates?.Dispose();
                m_HasResources?.Dispose();
                m_HasWorkStreams?.Dispose();
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
