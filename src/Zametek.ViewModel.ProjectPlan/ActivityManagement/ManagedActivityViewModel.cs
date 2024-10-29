using Avalonia.Data;
using ReactiveUI;
using System.Collections;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedActivityViewModel
        : ViewModelBase, IManagedActivityViewModel, IEditableObject, INotifyDataErrorInfo
    {
        #region Fields

        private readonly ICoreViewModel m_CoreViewModel;
        private DateTimeOffset? m_MinimumEarliestStartDateTime;
        private DateTimeOffset? m_MaximumLatestFinishDateTime;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly VertexGraphCompiler<int, int, int, IDependentActivity> m_VertexGraphCompiler;

        private readonly IDisposable? m_ProjectStartSub;
        private readonly IDisposable? m_ResourceSettingsSub;
        private readonly IDisposable? m_WorkStreamSettingsSub;
        private readonly IDisposable? m_DateTimeCalculatorCalculatorModeSub;
        private readonly IDisposable? m_DateTimeCalculatorDisplayModeSub;
        private readonly IDisposable? m_CompilationSub;

        private static readonly string[] s_NoErrors = [];
        private readonly Dictionary<string, List<string>> m_ErrorsByPropertyName;

        #endregion

        #region Ctors

        public ManagedActivityViewModel(
            ICoreViewModel coreViewModel,
            IDependentActivity dependentActivity,
            IDateTimeCalculator dateTimeCalculator,
            VertexGraphCompiler<int, int, int, IDependentActivity> vertexGraphCompiler,
            DateTimeOffset projectStart,
            IEnumerable<ActivityTrackerModel>? trackers,
            DateTimeOffset? minimumEarliestStartDateTime,
            DateTimeOffset? maximumLatestFinishDateTime)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(dependentActivity);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(vertexGraphCompiler);
            m_CoreViewModel = coreViewModel;
            DependentActivity = dependentActivity;
            m_DateTimeCalculator = dateTimeCalculator;
            m_ProjectStart = projectStart;
            m_MinimumEarliestStartDateTime = minimumEarliestStartDateTime;
            m_MaximumLatestFinishDateTime = maximumLatestFinishDateTime;
            m_VertexGraphCompiler = vertexGraphCompiler;
            m_ErrorsByPropertyName = [];
            ResourceSelector = new ResourceSelectorViewModel();
            m_ResourceSettings = m_CoreViewModel.ResourceSettings;
            RefreshResourceSelector();
            WorkStreamSelector = new WorkStreamSelectorViewModel();
            m_WorkStreamSettings = m_CoreViewModel.WorkStreamSettings;
            RefreshWorkStreamSelector();

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

            // We use Scheduler.CurrentThread here to ensure that wide sweeping changes,
            // such as settings changes, are performed in the same thread from the CoreViewModel.
            // That way they must complete before the CoreViewModel can proceed to compilation.

            m_ShowDates = this
                .WhenAnyValue(x => x.m_CoreViewModel.ShowDates)
                .ToProperty(this, x => x.ShowDates);

            m_ProjectStartSub = this
                .WhenAnyValue(x => x.m_CoreViewModel.ProjectStart)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(x => ProjectStart = x);

            m_ResourceSettingsSub = this
                .WhenAnyValue(x => x.m_CoreViewModel.ResourceSettings)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => ResourceSettings = x);

            m_WorkStreamSettingsSub = this
                .WhenAnyValue(x => x.m_CoreViewModel.WorkStreamSettings)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => WorkStreamSettings = x);

            m_DateTimeCalculatorCalculatorModeSub = this
                .ObservableForProperty(x => x.m_DateTimeCalculator.CalculatorMode)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(_ => UpdateEarliestStartAndLatestFinishDateTimes());

            m_DateTimeCalculatorDisplayModeSub = this
                .ObservableForProperty(x => x.m_DateTimeCalculator.DisplayMode)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(_ => RefreshStartAndFinishValues());

            m_CompilationSub = this
                .ObservableForProperty(x => x.m_CoreViewModel.GraphCompilation)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => SetAsCompiled());

            m_IsCompiled = false;
        }

        #endregion

        #region Properties

        private ResourceSettingsModel m_ResourceSettings;
        private ResourceSettingsModel ResourceSettings
        {
            get => m_ResourceSettings;
            set
            {
                m_ResourceSettings = value;
                SetNewTargetResources();
            }
        }

        private WorkStreamSettingsModel m_WorkStreamSettings;
        private WorkStreamSettingsModel WorkStreamSettings
        {
            get => m_WorkStreamSettings;
            set
            {
                m_WorkStreamSettings = value;
                SetNewTargetWorkStreams();
            }
        }

        public IDependentActivity DependentActivity { get; }

        public IResourceSelectorViewModel ResourceSelector { get; }

        public IWorkStreamSelectorViewModel WorkStreamSelector { get; }

        #endregion

        #region Private Methods

        private void SetMinimumEarliestStartTimes(int? input)
        {
            // Calculate integer and DateTimeOffset values (double pass).
            int? intValue = CalculateTime(input);
            DateTimeOffset? dateTimeOffsetValue = CalculateDateTime(intValue);

            dateTimeOffsetValue = CalculateDateTime(dateTimeOffsetValue);
            intValue = CalculateTime(dateTimeOffsetValue);

            // Validate integer value.
            ValidateMinimumEarliestStartTime(intValue);

            // Set integer and DateTimeOffset values.
            DependentActivity.MinimumEarliestStartTime = intValue;
            this.RaisePropertyChanged(nameof(MinimumEarliestStartTime));
            this.RaiseAndSetIfChanged(ref m_MinimumEarliestStartDateTime, dateTimeOffsetValue, nameof(MinimumEarliestStartDateTime));
            RefreshStartAndFinishValues();
        }

        private void SetMinimumEarliestStartTimes(DateTimeOffset? input, bool skipValidation = false)
        {
            // Calculate integer and DateTimeOffset values (double pass).
            DateTimeOffset? dateTimeOffsetValue = CalculateDateTime(input);
            int? intValue = CalculateTime(dateTimeOffsetValue);

            intValue = CalculateTime(intValue);
            dateTimeOffsetValue = CalculateDateTime(intValue);

            if (!skipValidation)
            {
                // Validate integer value.
                ValidateMinimumEarliestStartTime(intValue);
            }

            // Set integer and DateTimeOffset values.
            DependentActivity.MinimumEarliestStartTime = intValue;
            this.RaisePropertyChanged(nameof(MinimumEarliestStartTime));
            this.RaiseAndSetIfChanged(ref m_MinimumEarliestStartDateTime, dateTimeOffsetValue, nameof(MinimumEarliestStartDateTime));
            RefreshStartAndFinishValues();
        }

        private void SetMaximumLatestFinishTimes(int? input)
        {
            // Calculate integer and DateTimeOffset values (double pass).
            int? intValue = CalculateTime(input);
            DateTimeOffset? dateTimeOffsetValue = CalculateDateTime(intValue);

            dateTimeOffsetValue = CalculateDateTime(dateTimeOffsetValue);
            intValue = CalculateTime(dateTimeOffsetValue);

            // Validate integer value.
            ValidateMaximumLatestFinishTime(intValue);

            // Set integer and DateTimeOffset values.
            DependentActivity.MaximumLatestFinishTime = intValue;
            this.RaisePropertyChanged(nameof(MaximumLatestFinishTime));
            this.RaiseAndSetIfChanged(ref m_MaximumLatestFinishDateTime, dateTimeOffsetValue, nameof(MaximumLatestFinishDateTime));
            RefreshStartAndFinishValues();
        }

        private void SetMaximumLatestFinishTimes(DateTimeOffset? input, bool skipValidation = false)
        {
            // Calculate integer and DateTimeOffset values (double pass).
            DateTimeOffset? dateTimeOffsetValue = CalculateDateTime(input);
            int? intValue = CalculateTime(dateTimeOffsetValue);

            intValue = CalculateTime(intValue);
            dateTimeOffsetValue = CalculateDateTime(intValue);

            if (!skipValidation)
            {
                // Validate integer value.
                ValidateMaximumLatestFinishTime(intValue);
            }

            // Set integer and DateTimeOffset values.
            DependentActivity.MaximumLatestFinishTime = intValue;
            this.RaisePropertyChanged(nameof(MaximumLatestFinishTime));
            this.RaiseAndSetIfChanged(ref m_MaximumLatestFinishDateTime, dateTimeOffsetValue, nameof(MaximumLatestFinishDateTime));
            RefreshStartAndFinishValues();
        }

        private int? CalculateTime(DateTimeOffset? input)
        {
            int? result = null;
            if (input.HasValue)
            {
                result = m_DateTimeCalculator.CountDays(ProjectStart, input.GetValueOrDefault());
                result = CalculateTime(result);
            }
            return result;
        }

        private static int? CalculateTime(int? input)
        {
            int? result = input;
            if (result.HasValue && result < 0)
            {
                result = 0;
            }
            return result;
        }

        private DateTimeOffset? CalculateDateTime(int? input)
        {
            DateTimeOffset? result = null;
            if (input.HasValue)
            {
                result = m_DateTimeCalculator.AddDays(ProjectStart, input.GetValueOrDefault());
                result = CalculateDateTime(result);
            }
            return result;
        }

        private DateTimeOffset? CalculateDateTime(DateTimeOffset? input)
        {
            DateTimeOffset? result = input;
            if (result.HasValue)
            {
                if (result < ProjectStart)
                {
                    result = ProjectStart.DateTime;
                }
                result = new DateTimeOffset(result.GetValueOrDefault().Date + ProjectStart.TimeOfDay, ProjectStartTimeOffset);
            }
            return result;
        }

        private void ValidateMinimumEarliestStartTime(int? input)
        {
            //RemoveErrors(nameof(MinimumEarliestStartTime));
            //RemoveErrors(nameof(MinimumEarliestStartDateTime));
            string? errorMessage = ConstraintsValidationRule.Validate(MinimumFreeSlack, input, MaximumLatestFinishTime, Duration);
            if (errorMessage is not null)
            {
                //SetError(nameof(MinimumEarliestStartTime), errorMessage);
                //SetError(nameof(MinimumEarliestStartDateTime), errorMessage);
                throw new DataValidationException(errorMessage);
            }
        }

        private void ValidateMaximumLatestFinishTime(int? input)
        {
            //RemoveErrors(nameof(MaximumLatestFinishTime));
            //RemoveErrors(nameof(MaximumLatestFinishDateTime));
            string? errorMessage = ConstraintsValidationRule.Validate(MinimumFreeSlack, MinimumEarliestStartTime, input, Duration);
            if (errorMessage is not null)
            {
                //SetError(nameof(MaximumLatestFinishTime), errorMessage);
                //SetError(nameof(MaximumLatestFinishDateTime), errorMessage);
                throw new DataValidationException(errorMessage);
            }
        }

        private void UpdateActivityTargetResources()
        {
            DependentActivity.TargetResources.Clear();
            DependentActivity.TargetResources.UnionWith(ResourceSelector.SelectedResourceIds);
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
            IEnumerable<ResourceModel> targetResources = ResourceSettings.Resources.Select(x => x.CloneObject());
            ResourceSelector.SetTargetResources(targetResources, selectedTargetResources);
        }

        private void UpdateActivityTargetWorkStreams()
        {
            DependentActivity.TargetWorkStreams.Clear();
            DependentActivity.TargetWorkStreams.UnionWith(WorkStreamSelector.SelectedWorkStreamIds);
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
            IEnumerable<WorkStreamModel> targetWorkStreams = WorkStreamSettings.WorkStreams.Select(x => x.CloneObject());
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
        }




        private void SetAsCompiled()
        {
            m_IsCompiled = true;
            this.RaisePropertyChanged(nameof(AllocatedToResourcesString));
        }

        private void SetError(string propertyName, string error)
        {
            if (m_ErrorsByPropertyName.TryGetValue(propertyName, out var errorList))
            {
                if (!errorList.Contains(error))
                {
                    errorList.Add(error);
                }
            }
            else
            {
                m_ErrorsByPropertyName.Add(propertyName, [error]);
            }
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            this.RaisePropertyChanged(nameof(HasErrors));
        }

        private void RemoveErrors(string propertyName)
        {
            m_ErrorsByPropertyName.Remove(propertyName);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            this.RaisePropertyChanged(nameof(HasErrors));
        }

        #endregion

        #region IManagedActivityViewModel Members

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

        public TimeSpan ProjectStartTimeOffset => m_ProjectStart.Offset;

        public string DependenciesString
        {
            get => string.Join(DependenciesStringValidationRule.Separator, Dependencies.OrderBy(x => x));
            set
            {
                //RemoveErrors(nameof(DependenciesString)); // TODO
                (IEnumerable<int>? updatedDependencies, string? errorMessage) = DependenciesStringValidationRule.Validate(value, Id);
                if (errorMessage is not null)
                {
                    //SetError(nameof(DependenciesString), errorMessage); // TODO
                    throw new DataValidationException(errorMessage);
                }

                if (updatedDependencies is not null)
                {
                    m_VertexGraphCompiler.SetActivityDependencies(Id, new HashSet<int>(updatedDependencies));
                }
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(Dependencies));
            }
        }

        public string ResourceDependenciesString => string.Join(DependenciesStringValidationRule.Separator, ResourceDependencies.OrderBy(x => x));

        public int Id => DependentActivity.Id;

        public bool CanBeRemoved => DependentActivity.CanBeRemoved;

        public string Name
        {
            get => DependentActivity.Name;
            set
            {
                DependentActivity.Name = value;
                this.RaisePropertyChanged();
            }
        }

        public string Notes
        {
            get => DependentActivity.Notes;
            set
            {
                DependentActivity.Notes = value;
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
                DependentActivity.TargetResourceOperator = value;
                this.RaisePropertyChanged();
            }
        }

        public HashSet<int> AllocatedToResources => DependentActivity.AllocatedToResources;

        public string AllocatedToResourcesString
        {
            get
            {
                HashSet<int> allocatedToResources = AllocatedToResources;
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
                    DependentActivity.HasNoCost = value;
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

                //RemoveErrors(nameof(Duration));
                string? errorMessage = ConstraintsValidationRule.Validate(MinimumFreeSlack, MinimumEarliestStartTime, MaximumLatestFinishTime, value);
                if (errorMessage is not null)
                {
                    //SetError(nameof(Duration), errorMessage);
                    throw new DataValidationException(errorMessage);
                }

                DependentActivity.Duration = value;
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
                DependentActivity.FreeSlack = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(InterferingSlack));
                this.RaisePropertyChanged(nameof(DependenciesString));
                this.RaisePropertyChanged(nameof(ResourceDependenciesString));
            }
        }

        public int? InterferingSlack => DependentActivity.InterferingSlack;

        public bool IsCritical => DependentActivity.IsCritical;

        public int? EarliestStartTime
        {
            get => DependentActivity.EarliestStartTime;
            set
            {
                DependentActivity.EarliestStartTime = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(EarliestStartDateTimeOffset));
                this.RaisePropertyChanged(nameof(EarliestFinishTime));
                this.RaisePropertyChanged(nameof(EarliestFinishDateTimeOffset));
                this.RaisePropertyChanged(nameof(TotalSlack));
                this.RaisePropertyChanged(nameof(IsCritical));
                this.RaisePropertyChanged(nameof(InterferingSlack));
                this.RaisePropertyChanged(nameof(DependenciesString));
                this.RaisePropertyChanged(nameof(ResourceDependenciesString));
            }
        }

        public DateTimeOffset? EarliestStartDateTimeOffset
        {
            get
            {
                if (EarliestStartTime.HasValue)
                {
                    return m_DateTimeCalculator.AddDays(ProjectStart, EarliestStartTime.GetValueOrDefault());
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
                    return m_DateTimeCalculator.AddDays(ProjectStart, LatestStartTime.GetValueOrDefault());
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
                DependentActivity.LatestFinishTime = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(LatestFinishDateTimeOffset));
                this.RaisePropertyChanged(nameof(LatestStartTime));
                this.RaisePropertyChanged(nameof(LatestStartDateTimeOffset));
                this.RaisePropertyChanged(nameof(TotalSlack));
                this.RaisePropertyChanged(nameof(IsCritical));
                this.RaisePropertyChanged(nameof(InterferingSlack));
                this.RaisePropertyChanged(nameof(DependenciesString));
                this.RaisePropertyChanged(nameof(ResourceDependenciesString));
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

                //RemoveErrors(nameof(MinimumFreeSlack));
                string? errorMessage = ConstraintsValidationRule.Validate(value, MinimumEarliestStartTime, MaximumLatestFinishTime, Duration);
                if (errorMessage is not null)
                {
                    //SetError(nameof(MinimumFreeSlack), errorMessage);
                    throw new DataValidationException(errorMessage);
                }

                DependentActivity.MinimumFreeSlack = value;
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
            set => SetMinimumEarliestStartTimes(value);
        }

        public int? MaximumLatestFinishTime
        {
            get => DependentActivity.MaximumLatestFinishTime;
            set => SetMaximumLatestFinishTimes(value);
        }

        public DateTime? MaximumLatestFinishDateTime
        {
            get => m_MaximumLatestFinishDateTime?.DateTime;
            set => SetMaximumLatestFinishTimes(value);
        }

        public IActivityTrackerSetViewModel TrackerSet { get; }

        public List<ActivityTrackerModel> Trackers => TrackerSet.Trackers;

        public HashSet<int> Dependencies => DependentActivity.Dependencies;

        public HashSet<int> ResourceDependencies => DependentActivity.ResourceDependencies;

        public void SetAsReadOnly()
        {
            DependentActivity.SetAsReadOnly();
        }

        public void SetAsRemovable()
        {
            DependentActivity.SetAsRemovable();
        }

        public object CloneObject()
        {
            var activity = (IDependentActivity)DependentActivity.CloneObject();
            activity.Trackers.Clear();
            activity.Trackers.AddRange(Trackers);
            return activity;
        }

        #endregion

        #region IEditableObject Members

        private bool m_isDirty;

        public void BeginEdit()
        {
            // Bug Fix: Windows Controls call EndEdit twice; Once
            // from IEditableCollectionView, and once from BindingGroup.
            // This makes sure it only happens once after a BeginEdit.
            m_isDirty = true;
        }

        public void EndEdit()
        {
            if (m_isDirty)
            {
                m_isDirty = false;
                UpdateActivityTargetResources();
                UpdateActivityTargetWorkStreams();
                TrackerSet.RefreshIndex();
                m_CoreViewModel.IsProjectUpdated = true;
                IsCompiled = false;
            }
        }

        public void CancelEdit()
        {
            m_isDirty = false;
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_ProjectStartSub?.Dispose();
            m_ResourceSettingsSub?.Dispose();
            m_WorkStreamSettingsSub?.Dispose();
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
                // TODO: dispose managed state (managed objects).
                KillSubscriptions();
                TrackerSet.Dispose();
                m_ShowDates?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

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

        #region INotifyDataErrorInfo Members

        public bool HasErrors => m_ErrorsByPropertyName.Count != 0;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (!string.IsNullOrWhiteSpace(propertyName)
                && m_ErrorsByPropertyName.TryGetValue(propertyName, out var errorList))
            {
                return errorList;
            }
            return s_NoErrors;
        }

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        #endregion
    }
}
