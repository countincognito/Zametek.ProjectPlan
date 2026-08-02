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

        private readonly Lock m_Lock;
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
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_ManagedResourceViewModel = managedResourceViewModel;
            ResourceId = resourceId;
            m_ResourceActivitySelectorLookup = [];

            foreach (ResourceTrackerModel tracker in trackers)
            {
                if (tracker.ResourceId == ResourceId)
                {
                    var selector = new ResourceActivitySelectorViewModel(m_CoreViewModel, tracker);
                    m_ResourceActivitySelectorLookup[tracker.Time] = selector;
                }
            }

            SetLastResourceActivitySelector();

            SetTrackerIndexCommand = ReactiveCommand.Create<int?>(SetTrackerIndex);

            m_DaysSub = this
                .WhenAnyValue(
                    x => x.m_CoreViewModel.TrackerIndex,
                    x => x.m_CoreViewModel.IsReadyToReviseTrackers)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
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
        }

        #endregion

        #region IResourceTrackerViewModel Members

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
                    return TrackerSearchHelper.GetSearchSymbol(LastTrackerIndex, TrackerIndex);
                }
            }
        }

        public int? GetLastTrackerIndex(int activityId)
        {
            lock (m_Lock)
            {
                int? lastTrackerIndex = null;

                foreach (KeyValuePair<int, IResourceActivitySelectorViewModel> kvp in m_ResourceActivitySelectorLookup)
                {
                    if (kvp.Value.SelectedResourceActivityIds.Contains(activityId)
                        && (lastTrackerIndex is null || kvp.Key > lastTrackerIndex))
                    {
                        lastTrackerIndex = kvp.Key;
                    }
                }

                return lastTrackerIndex;
            }
        }

        public string GetSearchSymbol(int activityId)
        {
            lock (m_Lock)
            {
                return TrackerSearchHelper.GetSearchSymbol(GetLastTrackerIndex(activityId), TrackerIndex);
            }
        }


        public List<ResourceTrackerModel> CloneTrackers()
        {
            lock (m_Lock)
            {
                return [.. m_ResourceActivitySelectorLookup.Values
                    .Where(selector => selector.SelectedResourceActivityIds.Count > 0)
                    .OrderBy(selector => selector.Time)
                    .Select(selector =>
                    {
                        List<ResourceActivityTrackerModel> resourceActivityTrackers = [.. selector.SelectedTargetResourceActivities
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
                            })];

                        return new ResourceTrackerModel
                        {
                            Time = selector.Time,
                            ResourceId = selector.ResourceId,
                            ActivityTrackers = resourceActivityTrackers,
                        };
                    })];
            }
        }

        public void RefreshIndex()
        {
            lock (m_Lock)
            {
                // Clean up any selectors with zero selections.
                List<KeyValuePair<int, IResourceActivitySelectorViewModel>> toRemove = [.. m_ResourceActivitySelectorLookup.Where(kvp => kvp.Value.SelectedResourceActivityIds.Count == 0)];

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

        public IResourceActivitySelectorViewModel GetDay(int dayOffset)
        {
            return GetResourceActivitySelector(dayOffset);
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
                m_DaysSub?.Dispose();
                foreach (IResourceActivitySelectorViewModel selector in m_ResourceActivitySelectorLookup.Values)
                {
                    selector.Dispose();
                }
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
