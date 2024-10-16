using ReactiveUI;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ResourceTrackerSetViewModel
        : ViewModelBase, IResourceTrackerSetViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IManagedResourceViewModel m_ManagedResourceViewModel;
        private readonly Dictionary<int, IResourceActivitySelectorViewModel> m_ResourceActivitySelectorLookup;

        private IResourceActivitySelectorViewModel? m_LastResourceActivitySelector;

        private readonly IDisposable? m_DaysSub;

        #endregion

        #region Ctors

        public ResourceTrackerSetViewModel(
            ICoreViewModel coreViewModel,
            IManagedResourceViewModel managedResourceViewModel,
            int resourceId,
            IEnumerable<ResourceTrackerModel> trackers)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(managedResourceViewModel);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            m_ManagedResourceViewModel = managedResourceViewModel;
            ResourceId = resourceId;
            m_ResourceActivitySelectorLookup = [];

            foreach (ResourceTrackerModel tracker in trackers)
            {
                if (tracker.ResourceId == ResourceId)
                {
                    var selector = new ResourceActivitySelectorViewModel(m_CoreViewModel, tracker);
                    m_ResourceActivitySelectorLookup.TryAdd(tracker.Time, selector);
                }
            }

            SetLastResourceActivitySelector();

            SetTrackerIndexCommand = ReactiveCommand.Create<int?>(SetTrackerIndex);

            m_DaysSub = this
                .WhenAnyValue(
                    x => x.m_CoreViewModel.TrackerIndex,
                    x => x.m_CoreViewModel.IsReadyToReviseTrackers)
                .ObserveOn(RxApp.TaskpoolScheduler) // TODO check this will work.
                .Subscribe(_ => RefreshDays());
        }

        #endregion

        #region Private Members

        private int TrackerIndex => m_CoreViewModel.TrackerIndex;

        private IResourceActivitySelectorViewModel GetResourceActivitySelector(int index)
        {
            lock (m_Lock)
            {
                int indexOffset = index + TrackerIndex;

                if (!m_ResourceActivitySelectorLookup.TryGetValue(indexOffset, out IResourceActivitySelectorViewModel? selector))
                {
                    // If the selector does not exist, but we are currently editing
                    // the managed resource, then create a new selector and add it
                    // to the lookup dictionary.
                    if (m_ManagedResourceViewModel.IsEditing)
                    {
                        selector = new ResourceActivitySelectorViewModel(
                            m_CoreViewModel,
                            new ResourceTrackerModel
                            {
                                Time = indexOffset,
                                ResourceId = ResourceId,
                            });
                        m_ResourceActivitySelectorLookup.Add(indexOffset, selector);
                    }
                    // Otherwise, just return the empty one. Since we only need to
                    // create a new selector during editing.
                    else
                    {
                        selector = ResourceActivitySelectorViewModel.Empty;
                    }
                }
                return selector;
            }
        }

        private void SetLastResourceActivitySelector()
        {
            lock (m_Lock)
            {
                if (m_ResourceActivitySelectorLookup.Count == 0)
                {
                    m_LastResourceActivitySelector = null;
                }
                else
                {
                    m_LastResourceActivitySelector = m_ResourceActivitySelectorLookup.MaxBy(kvp => kvp.Key).Value;
                }
            }
        }

        private void SetTrackerIndex(int? trackerIndex)
        {
            lock (m_Lock)
            {
                if (trackerIndex is not null)
                {
                    m_CoreViewModel.TrackerIndex = trackerIndex.GetValueOrDefault();
                }
            }
        }

        private void RefreshDays()
        {
            RefreshIndex();
            this.RaisePropertyChanged(nameof(Day00));
            this.RaisePropertyChanged(nameof(Day01));
            this.RaisePropertyChanged(nameof(Day02));
            this.RaisePropertyChanged(nameof(Day03));
            this.RaisePropertyChanged(nameof(Day04));
            this.RaisePropertyChanged(nameof(Day05));
            this.RaisePropertyChanged(nameof(Day06));
            this.RaisePropertyChanged(nameof(Day07));
            this.RaisePropertyChanged(nameof(Day08));
            this.RaisePropertyChanged(nameof(Day09));
            this.RaisePropertyChanged(nameof(Day10));
            this.RaisePropertyChanged(nameof(Day11));
            this.RaisePropertyChanged(nameof(Day12));
            this.RaisePropertyChanged(nameof(Day13));
            this.RaisePropertyChanged(nameof(Day14));
        }

        #endregion

        #region IResourceTrackerViewModel Members

        public List<ResourceTrackerModel> Trackers
        {
            get
            {
                return m_ResourceActivitySelectorLookup.Values
                    .Where(selector => selector.SelectedResourceActivityIds.Count > 0)
                    .OrderBy(selector => selector.Time)
                    .Select(selector =>
                    {
                        List<ResourceActivityTrackerModel> resourceActivityTrackers = selector.SelectedTargetResourceActivities
                            .Select(activity =>
                            {
                                return new ResourceActivityTrackerModel
                                {
                                    Time = selector.Time,
                                    ResourceId = selector.ResourceId,
                                    ActivityId = activity.Id,
                                    ActivityName = activity.Name,
                                    PercentageWorked = activity.PercentageWorked,
                                };
                            }).ToList();

                        return new ResourceTrackerModel
                        {
                            Time = selector.Time,
                            ResourceId = selector.ResourceId,
                            ActivityTrackers = resourceActivityTrackers,
                        };
                    }).ToList();
            }
        }

        public int ResourceId { get; }

        public int? LastTrackerIndex
        {
            get
            {
                if (m_LastResourceActivitySelector is null)
                {
                    return null;
                }
                return m_LastResourceActivitySelector.Time;
            }
        }

        public ICommand SetTrackerIndexCommand { get; }

        public string SearchSymbol
        {
            get
            {
                lock (m_Lock)
                {
                    int? lastTrackerIndex = LastTrackerIndex;
                    int trackerIndex = TrackerIndex;
                    if (lastTrackerIndex is null)
                    {
                        return Resource.ProjectPlan.Symbols.Symbol_Nowhere;
                    }
                    if (lastTrackerIndex > trackerIndex)
                    {
                        return Resource.ProjectPlan.Symbols.Symbol_Forwards;
                    }
                    if (lastTrackerIndex < trackerIndex)
                    {
                        return Resource.ProjectPlan.Symbols.Symbol_Backwards;
                    }
                    return Resource.ProjectPlan.Symbols.Symbol_InPlace;
                }
            }
        }

        public void RefreshIndex()
        {
            lock (m_Lock)
            {
                // Clean up any selectors with zero selections.
                List<KeyValuePair<int, IResourceActivitySelectorViewModel>> toRemove = m_ResourceActivitySelectorLookup
                    .Where(kvp => kvp.Value.SelectedResourceActivityIds.Count == 0)
                    .ToList();

                foreach (KeyValuePair<int, IResourceActivitySelectorViewModel> kvp in toRemove)
                {
                    m_ResourceActivitySelectorLookup.Remove(kvp.Key);
                    kvp.Value.Dispose();
                }

                SetLastResourceActivitySelector();
                this.RaisePropertyChanged(nameof(LastTrackerIndex));
                this.RaisePropertyChanged(nameof(SearchSymbol));
            }
        }

        public IResourceActivitySelectorViewModel Day00
        {
            get => GetResourceActivitySelector(0);
        }

        public IResourceActivitySelectorViewModel Day01
        {
            get => GetResourceActivitySelector(1);
        }

        public IResourceActivitySelectorViewModel Day02
        {
            get => GetResourceActivitySelector(2);
        }

        public IResourceActivitySelectorViewModel Day03
        {
            get => GetResourceActivitySelector(3);
        }

        public IResourceActivitySelectorViewModel Day04
        {
            get => GetResourceActivitySelector(4);
        }

        public IResourceActivitySelectorViewModel Day05
        {
            get => GetResourceActivitySelector(5);
        }

        public IResourceActivitySelectorViewModel Day06
        {
            get => GetResourceActivitySelector(6);
        }

        public IResourceActivitySelectorViewModel Day07
        {
            get => GetResourceActivitySelector(7);
        }

        public IResourceActivitySelectorViewModel Day08
        {
            get => GetResourceActivitySelector(8);
        }

        public IResourceActivitySelectorViewModel Day09
        {
            get => GetResourceActivitySelector(9);
        }

        public IResourceActivitySelectorViewModel Day10
        {
            get => GetResourceActivitySelector(10);
        }

        public IResourceActivitySelectorViewModel Day11
        {
            get => GetResourceActivitySelector(11);
        }

        public IResourceActivitySelectorViewModel Day12
        {
            get => GetResourceActivitySelector(12);
        }

        public IResourceActivitySelectorViewModel Day13
        {
            get => GetResourceActivitySelector(13);
        }

        public IResourceActivitySelectorViewModel Day14
        {
            get => GetResourceActivitySelector(14);
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
                m_DaysSub?.Dispose();
                foreach (IResourceActivitySelectorViewModel selector in m_ResourceActivitySelectorLookup.Values)
                {
                    selector.Dispose();
                }
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
    }
}
