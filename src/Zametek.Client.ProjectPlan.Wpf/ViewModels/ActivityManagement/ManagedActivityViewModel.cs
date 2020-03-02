using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ManagedActivityViewModel
        : BindableBase, IDependentActivity<int>, IActivity<int>, IEditableObject
    {
        #region Fields

        private readonly IDependentActivity<int> m_DependentActivity;
        private DateTime m_ProjectStart;
        private bool m_HasUpdatedDependencies;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IEventAggregator m_EventService;

        #endregion

        #region Ctors

        private ManagedActivityViewModel(
            IDateTimeCalculator dateTimeCalculator,
            IEventAggregator eventService)
        {
            m_DateTimeCalculator = dateTimeCalculator ?? throw new ArgumentNullException(nameof(dateTimeCalculator));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            ResourceSelector = new ResourceSelectorViewModel();
            AllocatedToResources = new AllocatedToResourcesViewModel();
            UpdatedDependencies = new HashSet<int>();
        }

        public ManagedActivityViewModel(
            IDependentActivity<int> dependentActivity,
            DateTime projectStart,
            IEnumerable<Common.Project.v0_1_0.ResourceDto> targetResources,
            IDateTimeCalculator dateTimeCalculator,
            IEventAggregator eventService)
            : this(dateTimeCalculator, eventService)
        {
            m_DependentActivity = dependentActivity ?? throw new ArgumentNullException(nameof(dependentActivity));
            m_ProjectStart = projectStart;
            var selectedResources = new HashSet<int>(m_DependentActivity.TargetResources.ToList());
            ResourceSelector.SetTargetResources(targetResources, selectedResources);
            AllocatedToResources.SetAllocatedToResources(targetResources, new HashSet<int>());

            if (MinimumEarliestStartDateTime.HasValue)
            {
                CalculateMinimumEarliestStartTime();
            }
            else if (MinimumEarliestStartTime.HasValue)
            {
                CalculateMinimumEarliestStartDateTime();
            }
        }

        #endregion

        #region Properties

        public IDependentActivity<int> DependentActivity => m_DependentActivity;

        public DateTime ProjectStart
        {
            get
            {
                return m_ProjectStart;
            }
            set
            {
                m_ProjectStart = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(EarliestStartDateTime));
                RaisePropertyChanged(nameof(LatestStartDateTime));
                RaisePropertyChanged(nameof(EarliestFinishDateTime));
                RaisePropertyChanged(nameof(LatestFinishDateTime));
                CalculateMinimumEarliestStartTime();
                RaisePropertyChanged(nameof(MinimumEarliestStartTime));
            }
        }

        public bool UseBusinessDays
        {
            set
            {
                m_DateTimeCalculator.UseBusinessDays(value);
                RaisePropertyChanged(nameof(EarliestStartDateTime));
                RaisePropertyChanged(nameof(LatestStartDateTime));
                RaisePropertyChanged(nameof(EarliestFinishDateTime));
                RaisePropertyChanged(nameof(LatestFinishDateTime));
                CalculateMinimumEarliestStartTime();
                RaisePropertyChanged(nameof(MinimumEarliestStartTime));
            }
        }

        public ResourceSelectorViewModel ResourceSelector
        {
            get;
            private set;
        }

        public AllocatedToResourcesViewModel AllocatedToResources
        {
            get;
            private set;
        }

        public string DependenciesString
        {
            get
            {
                return string.Join(DependenciesStringValidationRule.Separator.ToString(), Dependencies.OrderBy(x => x));
            }
            set
            {
                string stripped = DependenciesStringValidationRule.StripWhitespace(value);
                IList<int> updatedDependencies = DependenciesStringValidationRule.Parse(stripped);
                UpdatedDependencies.Clear();
                UpdatedDependencies.UnionWith(updatedDependencies);
                HasUpdatedDependencies = true;
                RaisePropertyChanged();
            }
        }

        public HashSet<int> UpdatedDependencies
        {
            get;
        }

        public bool HasUpdatedDependencies
        {
            get
            {
                return m_HasUpdatedDependencies;
            }
            set
            {
                m_HasUpdatedDependencies = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(Dependencies));
                RaisePropertyChanged(nameof(DependenciesString));
            }
        }

        public string ResourceDependenciesString
        {
            get
            {
                return string.Join(DependenciesStringValidationRule.Separator.ToString(), ResourceDependencies.OrderBy(x => x));
            }
        }

        public DateTime? EarliestStartDateTime
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

        public DateTime? LatestStartDateTime
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

        public DateTime? EarliestFinishDateTime
        {
            get
            {
                if (EarliestFinishTime.HasValue)
                {
                    return m_DateTimeCalculator.AddDays(ProjectStart, EarliestFinishTime.GetValueOrDefault());
                }
                return null;
            }
        }

        public DateTime? LatestFinishDateTime
        {
            get
            {
                if (LatestFinishTime.HasValue)
                {
                    return m_DateTimeCalculator.AddDays(ProjectStart, LatestFinishTime.GetValueOrDefault());
                }
                return null;
            }
        }

        #endregion

        #region Public Methods

        public void SetTargetResources(IEnumerable<Common.Project.v0_1_0.ResourceDto> targetResources)
        {
            if (targetResources == null)
            {
                throw new ArgumentNullException(nameof(targetResources));
            }
            UpdateTargetResources();
            var selectedTargetResources = new HashSet<int>(m_DependentActivity.TargetResources.ToList());
            ResourceSelector.SetTargetResources(targetResources, selectedTargetResources);
            UpdateTargetResources();
        }

        public void SetAllocatedToResources(IEnumerable<Common.Project.v0_1_0.ResourceDto> targetResources, HashSet<int> allocatedToResources)
        {
            if (targetResources == null)
            {
                throw new ArgumentNullException(nameof(targetResources));
            }
            if (allocatedToResources == null)
            {
                throw new ArgumentNullException(nameof(allocatedToResources));
            }
            AllocatedToResources.SetAllocatedToResources(targetResources, allocatedToResources);
            UpdateAllocatedToResources();
        }

        #endregion

        #region Private Methods

        private void PublishManagedActivityUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<ManagedActivityUpdatedPayload>>()
                .Publish(new ManagedActivityUpdatedPayload());
        }

        private void CalculateMinimumEarliestStartDateTime()
        {
            int? minimumEarliestStartTime = m_DependentActivity.MinimumEarliestStartTime;
            if (minimumEarliestStartTime.HasValue)
            {
                m_DependentActivity.MinimumEarliestStartDateTime = m_DateTimeCalculator.AddDays(ProjectStart, minimumEarliestStartTime.GetValueOrDefault());
            }
            else
            {
                m_DependentActivity.MinimumEarliestStartDateTime = null;
            }
        }

        private void CalculateMinimumEarliestStartTime()
        {
            DateTime? minimumEarliestStartDateTime = m_DependentActivity.MinimumEarliestStartDateTime;
            if (minimumEarliestStartDateTime.HasValue)
            {
                m_DependentActivity.MinimumEarliestStartTime = m_DateTimeCalculator.CountDays(ProjectStart, minimumEarliestStartDateTime.GetValueOrDefault());
            }
            else
            {
                m_DependentActivity.MinimumEarliestStartTime = null;
            }
        }

        private void UpdateTargetResources()
        {
            m_DependentActivity.TargetResources.Clear();
            m_DependentActivity.TargetResources.UnionWith(ResourceSelector.SelectedResourceIds);
            RaisePropertyChanged(nameof(TargetResources));
            RaisePropertyChanged(nameof(ResourceSelector));
        }

        private void UpdateAllocatedToResources()
        {
            RaisePropertyChanged(nameof(AllocatedToResources));
        }

        #endregion

        #region IDependentActivity<int> Members

        public int Id => m_DependentActivity.Id;

        public bool CanBeRemoved => m_DependentActivity.CanBeRemoved;

        public string Name
        {
            get
            {
                return m_DependentActivity.Name;
            }
            set
            {
                m_DependentActivity.Name = value;
                RaisePropertyChanged();
            }
        }

        public HashSet<int> TargetResources => m_DependentActivity.TargetResources;

        public LogicalOperator TargetResourceOperator
        {
            get
            {
                return m_DependentActivity.TargetResourceOperator;
            }
            set
            {
                m_DependentActivity.TargetResourceOperator = value;
                RaisePropertyChanged();
            }
        }

        public bool IsDummy => m_DependentActivity.IsDummy;

        public int Duration
        {
            get
            {
                return m_DependentActivity.Duration;
            }
            set
            {
                if (value < 0)
                {
                    value = 0;
                }
                m_DependentActivity.Duration = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(IsDummy));
                RaisePropertyChanged(nameof(IsCritical));
                RaisePropertyChanged(nameof(EarliestFinishTime));
                RaisePropertyChanged(nameof(EarliestFinishDateTime));
                RaisePropertyChanged(nameof(LatestStartTime));
                RaisePropertyChanged(nameof(LatestStartDateTime));
                RaisePropertyChanged(nameof(TotalSlack));
                RaisePropertyChanged(nameof(InterferingSlack));
            }
        }

        public int? TotalSlack => m_DependentActivity.TotalSlack;

        public int? FreeSlack
        {
            get
            {
                return m_DependentActivity.FreeSlack;
            }
            set
            {
                m_DependentActivity.FreeSlack = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(InterferingSlack));
                RaisePropertyChanged(nameof(DependenciesString));
                RaisePropertyChanged(nameof(ResourceDependenciesString));
            }
        }

        public int? InterferingSlack => m_DependentActivity.InterferingSlack;

        public bool IsCritical => m_DependentActivity.IsCritical;

        public int? EarliestStartTime
        {
            get
            {
                return m_DependentActivity.EarliestStartTime;
            }
            set
            {
                m_DependentActivity.EarliestStartTime = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(EarliestStartDateTime));
                RaisePropertyChanged(nameof(EarliestFinishTime));
                RaisePropertyChanged(nameof(EarliestFinishDateTime));
                RaisePropertyChanged(nameof(TotalSlack));
                RaisePropertyChanged(nameof(IsCritical));
                RaisePropertyChanged(nameof(InterferingSlack));
                RaisePropertyChanged(nameof(DependenciesString));
                RaisePropertyChanged(nameof(ResourceDependenciesString));
            }
        }

        public int? LatestStartTime => m_DependentActivity.LatestStartTime;

        public int? EarliestFinishTime => m_DependentActivity.EarliestFinishTime;

        public int? LatestFinishTime
        {
            get
            {
                return m_DependentActivity.LatestFinishTime;
            }
            set
            {
                m_DependentActivity.LatestFinishTime = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(LatestFinishDateTime));
                RaisePropertyChanged(nameof(LatestStartTime));
                RaisePropertyChanged(nameof(LatestStartDateTime));
                RaisePropertyChanged(nameof(TotalSlack));
                RaisePropertyChanged(nameof(IsCritical));
                RaisePropertyChanged(nameof(InterferingSlack));
                RaisePropertyChanged(nameof(DependenciesString));
                RaisePropertyChanged(nameof(ResourceDependenciesString));
            }
        }

        public int? MinimumFreeSlack
        {
            get
            {
                return m_DependentActivity.MinimumFreeSlack;
            }
            set
            {
                if (value < 0)
                {
                    value = 0;
                }
                m_DependentActivity.MinimumFreeSlack = value;
                RaisePropertyChanged();
            }
        }

        public int? MinimumEarliestStartTime
        {
            get
            {
                return m_DependentActivity.MinimumEarliestStartTime;
            }
            set
            {
                if (value < 0)
                {
                    value = 0;
                }
                m_DependentActivity.MinimumEarliestStartTime = value;
                RaisePropertyChanged();
                CalculateMinimumEarliestStartDateTime();
                RaisePropertyChanged(nameof(MinimumEarliestStartDateTime));
            }
        }

        public DateTime? MinimumEarliestStartDateTime
        {
            get
            {
                return m_DependentActivity.MinimumEarliestStartDateTime;
            }
            set
            {
                if (value.HasValue)
                {
                    if (value < ProjectStart)
                    {
                        value = ProjectStart;
                    }
                    value = value.GetValueOrDefault().Date + ProjectStart.TimeOfDay;
                }
                m_DependentActivity.MinimumEarliestStartDateTime = value;
                RaisePropertyChanged();
                CalculateMinimumEarliestStartTime();
                RaisePropertyChanged(nameof(MinimumEarliestStartTime));
            }
        }

        public HashSet<int> Dependencies => m_DependentActivity.Dependencies;

        public HashSet<int> ResourceDependencies => m_DependentActivity.ResourceDependencies;

        public void SetAsReadOnly()
        {
            m_DependentActivity.SetAsReadOnly();
        }

        public void SetAsRemovable()
        {
            m_DependentActivity.SetAsRemovable();
        }

        public object CloneObject()
        {
            return m_DependentActivity.CloneObject();
        }

        #endregion

        #region IEditableObject Members

        private bool _isDirty = false;

        public void BeginEdit()
        {
            // Bug Fix: Windows Controls call EndEdit twice; Once
            // from IEditableCollectionView, and once from BindingGroup.
            // This makes sure it only happens once after a BeginEdit.
            _isDirty = true;
        }

        public void EndEdit()
        {
            if (_isDirty)
            {
                _isDirty = false;
                UpdateTargetResources();
                PublishManagedActivityUpdatedPayload();
            }
        }

        public void CancelEdit()
        {
            _isDirty = false;
        }

        #endregion
    }
}
