using ReactiveUI;
using System.Collections.Specialized;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ResourceActivitySelectorViewModel
        : ViewModelBase, IResourceActivitySelectorViewModel
    {
        #region Fields

        private readonly Lock m_Lock;
        private readonly ICoreViewModel m_CoreViewModel;
        private readonly int m_ResourceId;
        private readonly int m_Time;
        private static readonly EqualityComparer<ISelectableResourceActivityViewModel> s_EqualityComparer =
            EqualityComparer<ISelectableResourceActivityViewModel>.Create(
                    (x, y) =>
                    {
                        if (x is null)
                        {
                            return false;
                        }
                        if (y is null)
                        {
                            return false;
                        }
                        return x.Id == y.Id;
                    },
                    x => x.Id);

        private static readonly Comparer<ISelectableResourceActivityViewModel> s_SortComparer =
            Comparer<ISelectableResourceActivityViewModel>.Create(
                    (x, y) =>
                    {
                        if (x is null)
                        {
                            if (y is null)
                            {
                                return 0;
                            }
                            return -1;
                        }
                        if (y is null)
                        {
                            return 1;
                        }

                        return x.Id.CompareTo(y.Id);
                    });

        // The lists themselves, which only a holder of m_Lock reads or changes. Each reader gets an array instead (see the
        // properties). A revision changes the lists on the thread that asks for it, and the cells of the timesheet read them on
        // the UI thread (ARCHITECTURE section 7 rule 17).
        private readonly ObservableUniqueCollection<ISelectableResourceActivityViewModel> m_TargetResourceActivities;
        private readonly ObservableUniqueCollection<ISelectableResourceActivityViewModel> m_SelectedTargetResourceActivities;

        // Set when a list changes, and cleared when its array is made again. Only a holder of m_Lock uses them.
        private bool m_AreTargetsChanged;
        private bool m_IsSelectionChanged;

        private readonly IDisposable? m_ReviseResourceActivityTrackersSub;

        #endregion

        #region Ctors

        public ResourceActivitySelectorViewModel(
            ICoreViewModel coreViewModel,
            ResourceTrackerModel resourceTrackerModel)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(resourceTrackerModel);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_TargetResourceActivities = new(s_EqualityComparer);
            m_SelectedTargetResourceActivities = new(s_EqualityComparer);

            m_Time = resourceTrackerModel.Time;
            m_ResourceId = resourceTrackerModel.ResourceId;

            m_TargetResourceActivities.CollectionChanged += TargetResourceActivities_CollectionChanged;
            m_SelectedTargetResourceActivities.CollectionChanged += SelectedTargetResourceActivities_CollectionChanged;

            // Before the read of the activities: a request after that read gives a revision (ARCHITECTURE section 9).
            int trackersRevision = m_CoreViewModel.TrackersRevision;

            // Initial set up.
            ReviseTrackers(resourceTrackerModel.ActivityTrackers);

            // This needs to be on the current thread because all the tracker updates
            // need to be completed before a compilation can start. The value that a
            // chain gives when it subscribes says nothing new, unless a request came
            // after the read above: the constructor has read the activities.
            m_ReviseResourceActivityTrackersSub = this
                .WhenAnyValue(x => x.m_CoreViewModel.TrackersRevision)
                .SkipWhile(revision => revision == trackersRevision)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(_ => ReviseTrackers());
        }

        #endregion

        #region Private Members

        private void TargetResourceActivities_CollectionChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            m_AreTargetsChanged = true;
        }

        private void SelectedTargetResourceActivities_CollectionChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            m_IsSelectionChanged = true;
        }

        // Makes the arrays of the lists that changed again. Hold m_Lock. A list that did not change keeps its array, and so a
        // revision with nothing new makes nothing: a plan with bookings has a day for each booked day of each resource.
        private bool PublishLists()
        {
            bool isPublished = false;

            if (m_AreTargetsChanged)
            {
                m_AreTargetsChanged = false;
                m_TargetResourceActivitiesSnapshot = [.. m_TargetResourceActivities];
                isPublished = true;
            }

            if (m_IsSelectionChanged)
            {
                m_IsSelectionChanged = false;
                m_SelectedTargetResourceActivitiesSnapshot = [.. m_SelectedTargetResourceActivities];
                m_SelectedResourceActivityIds = [.. m_SelectedTargetResourceActivities.Select(x => x.Id)];
                isPublished = true;
            }

            return isPublished;
        }

        // After the release of m_Lock (ARCHITECTURE section 7 rule 6).
        private void RaiseListsChanged()
        {
            this.RaisePropertyChanged(nameof(TargetResourceActivities));
            this.RaisePropertyChanged(nameof(SelectedTargetResourceActivities));
        }

        private void ReviseTrackers(IList<ResourceActivityTrackerModel> resourceActivityTrackers)
        {
            HashSet<int> selectedResourceActivityIds = [.. resourceActivityTrackers.Select(x => x.ActivityId)];

            Dictionary<int, ResourceActivityTrackerModel> resourceActivityTrackerLookup = resourceActivityTrackers.ToDictionary(x => x.ActivityId);

            List<ResourceActivityTrackerModel> newResourceActivityTrackers =
                [.. m_CoreViewModel.RawActivities
                .Select(activity => new ResourceActivityTrackerModel
                {
                    Time = m_Time,
                    ResourceId = m_ResourceId,
                    ActivityId = activity.Id,
                    ActivityName = activity.Name ?? string.Empty,
                    PercentageWorked = resourceActivityTrackerLookup.TryGetValue(activity.Id, out ResourceActivityTrackerModel? existingTracker) ? existingTracker.PercentageWorked : 0,
                })];

            foreach (ResourceActivityTrackerModel resourceActivityTracker in newResourceActivityTrackers)
            {
                resourceActivityTrackerLookup[resourceActivityTracker.ActivityId] = resourceActivityTracker;
            }

            SetTargetResourceActivities(
                resourceActivityTrackerLookup.Values,
                selectedResourceActivityIds);
        }

        private void ReviseTrackers()
        {
            List<ResourceActivityTrackerModel> newResourceActivityTrackers =
                [.. m_CoreViewModel.RawActivities
                .Select(activity => new ResourceActivityTrackerModel
                {
                    Time = m_Time,
                    ResourceId = m_ResourceId,
                    ActivityId = activity.Id,
                    ActivityName = activity.Name ?? string.Empty,
                    PercentageWorked = 0
                })];

            bool isPublished;

            // The read of the bookings and the change are one step, and so an edit in a cell cannot come between them and be lost.
            lock (m_Lock)
            {
                isPublished = SetTargets(
                    newResourceActivityTrackers,
                    [.. m_SelectedTargetResourceActivities.Select(x => x.Id)]);
            }

            if (isPublished)
            {
                RaiseListsChanged();
            }
        }

        // Hold m_Lock.
        private bool SetTargets(
            IEnumerable<ResourceActivityTrackerModel> targetResourceActivities,
            HashSet<int> selectedTargetResourceActivities)
        {
            {
                // Find target view models that have been removed.
                List<ISelectableResourceActivityViewModel> removedViewModels = [.. m_TargetResourceActivities
                    .ExceptBy(targetResourceActivities.Select(x => x.ActivityId), x => x.Id)];

                // Delete the removed items from the target and selected collections.
                foreach (ISelectableResourceActivityViewModel vm in removedViewModels)
                {
                    m_TargetResourceActivities.Remove(vm);
                    m_SelectedTargetResourceActivities.Remove(vm);
                }

                // Find the selected view models that have been removed.
                List<ISelectableResourceActivityViewModel> removedSelectedViewModels = [.. m_SelectedTargetResourceActivities
                    .ExceptBy(selectedTargetResourceActivities, x => x.Id)];

                // Delete the removed selected items from the selected collections.
                foreach (ISelectableResourceActivityViewModel vm in removedSelectedViewModels)
                {
                    m_SelectedTargetResourceActivities.Remove(vm);
                }
            }
            {
                // Find the target models that have been added.
                List<ResourceActivityTrackerModel> addedModels = [.. targetResourceActivities
                    .ExceptBy(m_TargetResourceActivities.Select(x => x.Id), x => x.ActivityId)];

                // Create a collection of new view models.
                foreach (ResourceActivityTrackerModel model in addedModels)
                {
                    var vm = new SelectableResourceActivityViewModel(
                        model.ActivityId,
                        model.ActivityName,
                        model.PercentageWorked);

                    m_TargetResourceActivities.Add(vm);
                    if (selectedTargetResourceActivities.Contains(model.ActivityId))
                    {
                        m_SelectedTargetResourceActivities.Add(vm);
                    }
                }
            }
            {
                // Update names.
                Dictionary<int, ResourceActivityTrackerModel> targetResourceActivityLookup = targetResourceActivities.ToDictionary(x => x.ActivityId);

                foreach (ISelectableResourceActivityViewModel vm in m_TargetResourceActivities)
                {
                    if (targetResourceActivityLookup.TryGetValue(vm.Id, out ResourceActivityTrackerModel? value))
                    {
                        vm.Name = value.ActivityName;
                    }
                }
            }

            m_TargetResourceActivities.Sort(s_SortComparer);
            return PublishLists();
        }

        #endregion

        #region Properties

        public int ResourceId => m_ResourceId;

        public int Time => m_Time;

        public static IResourceActivitySelectorViewModel Empty { get; } = new EmptyResourceActivitySelectorViewModel();

        // Lock-free arrays of the lists, which PublishLists makes again under m_Lock
        // when a list changes. A reader on any thread gets the whole of one array,
        // and a change never meets the read. Getters that participate in change
        // notification must never take m_Lock (ReactiveUI re-reads them under its
        // own sink gate - see the deadlock note in GanttActivitySelectorViewModel);
        // the ids are also read inside ResourceTrackerSetViewModel's locked regions,
        // so the arrays remove a nested cross-object lock as well. They are arrays
        // so accidental mutation by a caller fails fast.
        private ISelectableResourceActivityViewModel[] m_TargetResourceActivitiesSnapshot = [];
        public IReadOnlyList<ISelectableResourceActivityViewModel> TargetResourceActivities => m_TargetResourceActivitiesSnapshot;

        private ISelectableResourceActivityViewModel[] m_SelectedTargetResourceActivitiesSnapshot = [];
        public IReadOnlyList<ISelectableResourceActivityViewModel> SelectedTargetResourceActivities => m_SelectedTargetResourceActivitiesSnapshot;

        private int[] m_SelectedResourceActivityIds = [];
        public IList<int> SelectedResourceActivityIds => m_SelectedResourceActivityIds;

        #endregion

        #region Public IResourceActivitySelectorViewModel

        public void SetTargetResourceActivities(
            IEnumerable<ResourceActivityTrackerModel> targetResourceActivities,
            HashSet<int> selectedTargetResourceActivities)
        {
            ArgumentNullException.ThrowIfNull(targetResourceActivities);
            ArgumentNullException.ThrowIfNull(selectedTargetResourceActivities);
            bool isPublished;

            lock (m_Lock)
            {
                isPublished = SetTargets(targetResourceActivities, selectedTargetResourceActivities);
            }

            if (isPublished)
            {
                RaiseListsChanged();
            }
        }

        public bool SetPercentageWorked(int activityId, int? percentageWorked)
        {
            bool isPublished;

            lock (m_Lock)
            {
                ISelectableResourceActivityViewModel? target = m_TargetResourceActivities.FirstOrDefault(x => x.Id == activityId);

                if (target is null)
                {
                    return false;
                }

                if (percentageWorked is null)
                {
                    m_SelectedTargetResourceActivities.Remove(target);
                }
                else
                {
                    // The value first, so that a reader that finds the booking finds its value.
                    target.PercentageWorked = percentageWorked.GetValueOrDefault();
                    m_SelectedTargetResourceActivities.Add(target);
                }

                isPublished = PublishLists();
            }

            if (isPublished)
            {
                RaiseListsChanged();
            }

            return true;
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
                m_ReviseResourceActivityTrackersSub?.Dispose();
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

    public class EmptyResourceActivitySelectorViewModel
        : IResourceActivitySelectorViewModel
    {
        public int ResourceId => default;

        public int Time => default;

        internal EmptyResourceActivitySelectorViewModel()
        {
            TargetResourceActivities = [];
            SelectedTargetResourceActivities = [];
            SelectedResourceActivityIds = [];
        }

        public IReadOnlyList<ISelectableResourceActivityViewModel> TargetResourceActivities { get; init; }

        public IReadOnlyList<ISelectableResourceActivityViewModel> SelectedTargetResourceActivities { get; init; }

        public IList<int> SelectedResourceActivityIds { get; init; }

        public void SetTargetResourceActivities(
            IEnumerable<ResourceActivityTrackerModel> targetResourceActivities,
            HashSet<int> selectedTargetResourceActivities)
        {
        }

        public bool SetPercentageWorked(int activityId, int? percentageWorked) => false;

        #region IDisposable Members

        private bool m_Disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
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
