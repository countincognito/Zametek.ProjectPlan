using ReactiveUI;
using System.Collections;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedPlanViewModel
        : ViewModelBase, IManagedPlanViewModel, IEditableObject, INotifyDataErrorInfo
    {
        #region Fields

        private static readonly string[] s_NoErrors = [];
        private readonly Dictionary<string, List<string>> m_ErrorsByPropertyName;

        #endregion

        #region Ctors

        public ManagedPlanViewModel(ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(projectPlan);
            ProjectPlan = projectPlan;
        }

        #endregion

        #region Properties

        public ProjectPlanModel ProjectPlan { get; }

        #endregion

        #region Private Methods

        private void SetError(string propertyName, string error)
        {
            if (m_ErrorsByPropertyName.TryGetValue(propertyName, out List<string>? errorList))
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

        private void ClearErrors(string? propertyName)
        {
            if (!string.IsNullOrWhiteSpace(propertyName)
                && m_ErrorsByPropertyName.TryGetValue(propertyName, out List<string>? errorList))
            {
                errorList.Clear();
            }
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        private void ClearErrors()
        {
            IList<string> propertyNames = [.. m_ErrorsByPropertyName.Keys];
            m_ErrorsByPropertyName.Clear();

            foreach (string propertyName in propertyNames)
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }

            this.RaisePropertyChanged(nameof(HasErrors));
        }

        #endregion

        #region IManagedPlanViewModel Members







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
                (IEnumerable<int>? updatedDependencies, string? errorMessage) = DependenciesStringValidationRule.Validate(value, Id);
                //if (errorMessage is not null)
                //{
                //    SetError(nameof(DependenciesString), errorMessage);
                //}

                if (updatedDependencies is not null)
                {
                    m_VertexGraphCompiler.SetActivityDependencies(Id, [.. updatedDependencies], PlanningDependencies);
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
                (IEnumerable<int>? updatedPlanningDependencies, string? errorMessage) = DependenciesStringValidationRule.Validate(value, Id);
                //if (errorMessage is not null)
                //{
                //    SetError(nameof(DependenciesString), errorMessage);
                //}

                if (updatedPlanningDependencies is not null)
                {
                    m_VertexGraphCompiler.SetActivityDependencies(Id, Dependencies, [.. updatedPlanningDependencies]);
                }
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(PlanningDependencies));
            }
        }

        public string ResourceDependenciesString => string.Join(DependenciesStringValidationRule.Separator, ResourceDependencies.OrderBy(x => x));

        public string SuccessorsString => string.Join(DependenciesStringValidationRule.Separator, Successors.OrderBy(x => x));

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
                    DependentActivity.HasNoCost = value;
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
                    DependentActivity.HasNoBilling = value;
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
                    DependentActivity.HasNoEffort = value;
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
                    DependentActivity.HasNoRisk = value;
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
                DependentActivity.EarliestStartTime = value;
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
                DependentActivity.LatestFinishTime = value;
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
                DateTimeOffset? input = value;

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

        public IActivityTrackerSetViewModel TrackerSet { get; }

        public List<ActivityTrackerModel> Trackers => TrackerSet.Trackers;

        public HashSet<int> Dependencies => DependentActivity.Dependencies;

        public HashSet<int> PlanningDependencies => DependentActivity.PlanningDependencies;

        public HashSet<int> ResourceDependencies => DependentActivity.ResourceDependencies;

        public HashSet<int> Successors => DependentActivity.Successors;

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
                m_HasResources?.Dispose();
                m_HasWorkStreams?.Dispose();
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
                && m_ErrorsByPropertyName.TryGetValue(propertyName, out List<string>? errorList))
            {
                return errorList;
            }
            return s_NoErrors;
        }

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        #endregion
    }
}
