using ReactiveUI;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class GanttActivitySelectorViewModel
        : ViewModelBase, IActivitySelectorViewModel
    {
        #region Fields

        private readonly Lock m_Lock;
        private int m_RevisingCount;
        private readonly ICoreViewModel m_CoreViewModel;

        private static readonly EqualityComparer<ISelectableActivityViewModel> s_EqualityComparer =
            EqualityComparer<ISelectableActivityViewModel>.Create(
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

        private static readonly Comparer<ISelectableActivityViewModel> s_SortComparer =
            Comparer<ISelectableActivityViewModel>.Create(
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

        private readonly IDisposable? m_ReviseActivitiesSub;
        private readonly IDisposable? m_ShowConnectionsSub;

        #endregion

        #region Ctors

        public GanttActivitySelectorViewModel(ICoreViewModel coreViewModel)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            m_Lock = new();
            m_RevisingCount = 0;
            m_CoreViewModel = coreViewModel;
            m_TargetActivities = new(s_EqualityComparer);
            m_ReadOnlyTargetActivities = new(m_TargetActivities);
            m_SelectedTargetActivities = new(s_EqualityComparer);

            m_SelectedTargetActivities.CollectionChanged += SelectedTargetActivities_CollectionChanged;

            // Initial set up.
            ReviseActivities();

            // This needs to be on the current thread because all the tracker updates
            // need to be completed before a compilation can start.
            m_ReviseActivitiesSub = this
                .WhenAnyValue(x => x.m_CoreViewModel.IsReadyToReviseTrackers)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(isReadyToRevise =>
                {
                    if (isReadyToRevise == ReadyToRevise.Yes)
                    {
                        try
                        {
                            // Guard the revision so any resulting selection changes
                            // are not mistaken for user edits and do not mark the
                            // project scenario as updated. The guard is a ref-count
                            // because revisions overlap across threads (this one
                            // runs on the scenario-load worker, the connections one
                            // on the UI thread): a shared bool let one reviser's
                            // finally clear the other's guard mid-revision, leaking
                            // a machine-driven change out as a user edit
                            // (dump-proven 2026-08-17).
                            Interlocked.Increment(ref m_RevisingCount);
                            ReviseActivities();
                        }
                        finally
                        {
                            Interlocked.Decrement(ref m_RevisingCount);
                        }
                    }
                });

            m_ShowConnectionsSub = this
                .WhenAnyValue(
                    rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.IsReadyToReviseGanttChartShowConnections)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(isReadyToRevise =>
                {
                    if (isReadyToRevise == ReadyToRevise.Yes)
                    {
                        try
                        {
                            Interlocked.Increment(ref m_RevisingCount);
                            ReviseActivities();
                            SetSelectedTargetActivities(
                                [.. m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowConnections]);
                            m_CoreViewModel.DisplaySettingsViewModel.IsReadyToReviseGanttChartShowConnections = ReadyToRevise.No;
                        }
                        finally
                        {
                            Interlocked.Decrement(ref m_RevisingCount);
                        }
                    }
                });
        }

        #endregion

        #region Properties

        private readonly ObservableUniqueCollection<ISelectableActivityViewModel> m_TargetActivities;
        private readonly ReadOnlyObservableCollection<ISelectableActivityViewModel> m_ReadOnlyTargetActivities;
        public ReadOnlyObservableCollection<ISelectableActivityViewModel> TargetActivities => m_ReadOnlyTargetActivities;

        // Use ObservableUniqueCollection to prevent selected
        // items appearing twice in the Urse MultiComboBox.
        private readonly ObservableUniqueCollection<ISelectableActivityViewModel> m_SelectedTargetActivities;
        public ObservableCollection<ISelectableActivityViewModel> SelectedTargetActivities => m_SelectedTargetActivities;

        // Lock-free snapshots of the values derived from the selection,
        // recomputed under m_Lock by RefreshDerivedProperties whenever the
        // selection changes. Getters that participate in change notification
        // must never take m_Lock: ReactiveUI re-reads them while holding its
        // own sink gate, so a locked getter here - combined with a raise made
        // while m_Lock was held on another thread - deadlocked the app
        // against the GanttChartManagerViewModel WhenAnyValue chain (caught
        // live in a dump, 2026-08-17). The id snapshot is an array so any
        // caller that tried to mutate it would fail fast instead of
        // corrupting shared state.
        private string m_TargetActivitiesString = string.Empty;
        private int[] m_SelectedActivityIds = [];

        public string TargetActivitiesString => m_TargetActivitiesString;

        public IList<int> SelectedActivityIds => m_SelectedActivityIds;

        #endregion

        #region Private Members

        private void SelectedTargetActivities_CollectionChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            // This handler IS the notification that the selection just
            // changed, and the write-through below reads the id snapshot -
            // so refresh the snapshots first.
            RefreshDerivedProperties();

            // Write the selection through to the display settings so the
            // connections filter persists with the project scenario - but only
            // for genuine user edits. While revising (settings-driven refreshes
            // and seeding) the persisted list is the authority: writing back
            // the transient selection state would clobber it (e.g. wiping a
            // freshly remapped filter during an activity renumber, where the
            // selector briefly empties out).
            if (m_RevisingCount == 0)
            {
                m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowConnections.Clear();
                m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowConnections.AddRange(SelectedActivityIds);
                m_CoreViewModel.DisplaySettingsViewModel.SetIsProjectScenarioUpdated(true);
            }
            RaiseTargetActivitiesPropertiesChanged();
        }

        private void ReviseActivities()
        {
            lock (m_Lock)
            {
                List<TargetActivityModel> newActivities =
                    [.. m_CoreViewModel.RawActivities
                        .Select(activity => new TargetActivityModel
                        {
                            Id = activity.Id,
                            Name = activity.Name ?? string.Empty
                        })];

                SetTargetActivities(
                    newActivities,
                    [.. SelectedActivityIds]);
            }
        }

        private void RefreshDerivedProperties()
        {
            lock (m_Lock)
            {
                m_TargetActivitiesString = string.Join(
                    DependenciesStringValidationRule.Separator,
                    SelectedTargetActivities.Select(x => x.DisplayName));
                m_SelectedActivityIds = [.. SelectedTargetActivities.Select(x => x.Id)];
            }
        }

        #endregion

        #region Public Members

        public string GetAllocatedToActivitiesString(HashSet<int> allocatedToActivities)
        {
            ArgumentNullException.ThrowIfNull(allocatedToActivities);
            lock (m_Lock)
            {
                return string.Join(
                    DependenciesStringValidationRule.Separator,
                    TargetActivities.Where(x => allocatedToActivities.Contains(x.Id))
                        .OrderBy(x => x.Id)
                        .Select(x => x.DisplayName));
            }
        }

        public void SetTargetActivities(
            IEnumerable<TargetActivityModel> targetActivities,
            HashSet<int> selectedTargetActivities)
        {
            ArgumentNullException.ThrowIfNull(targetActivities);
            ArgumentNullException.ThrowIfNull(selectedTargetActivities);
            lock (m_Lock)
            {
                {
                    // Find target view models that have been removed.
                    List<ISelectableActivityViewModel> removedViewModels = [.. m_TargetActivities.ExceptBy(targetActivities.Select(x => x.Id), x => x.Id)];

                    // Delete the removed items from the target and selected collections.
                    foreach (ISelectableActivityViewModel vm in removedViewModels)
                    {
                        m_TargetActivities.Remove(vm);
                        m_SelectedTargetActivities.Remove(vm);
                    }

                    // Find the selected view models that have been removed.
                    List<ISelectableActivityViewModel> removedSelectedViewModels = [.. m_SelectedTargetActivities.ExceptBy(selectedTargetActivities, x => x.Id)];

                    // Delete the removed selected items from the selected collections.
                    foreach (ISelectableActivityViewModel vm in removedSelectedViewModels)
                    {
                        m_SelectedTargetActivities.Remove(vm);
                    }
                }
                {
                    // Find the target models that have been added.
                    List<TargetActivityModel> addedModels = [.. targetActivities.ExceptBy(m_TargetActivities.Select(x => x.Id), x => x.Id)];

                    List<ISelectableActivityViewModel> addedViewModels = [];

                    // Create a collection of new view models.
                    foreach (TargetActivityModel model in addedModels)
                    {
                        var vm = new SelectableActivityViewModel(model.Id, model.Name);

                        m_TargetActivities.Add(vm);
                        if (selectedTargetActivities.Contains(model.Id))
                        {
                            m_SelectedTargetActivities.Add(vm);
                        }
                    }
                }
                {
                    // Update names.
                    Dictionary<int, TargetActivityModel> targetActivityLookup = targetActivities.ToDictionary(x => x.Id);

                    foreach (ISelectableActivityViewModel vm in m_TargetActivities)
                    {
                        if (targetActivityLookup.TryGetValue(vm.Id, out TargetActivityModel? value))
                        {
                            vm.Name = value.Name;
                        }
                    }
                }

                m_TargetActivities.Sort(s_SortComparer);
            }
            RaiseTargetActivitiesPropertiesChanged();
        }

        public void SetSelectedTargetActivities(HashSet<int> selectedTargetActivities)
        {
            ArgumentNullException.ThrowIfNull(selectedTargetActivities);
            lock (m_Lock)
            {
                m_SelectedTargetActivities.Clear();
                Dictionary<int, ISelectableActivityViewModel> targetActivityLookup = m_TargetActivities.ToDictionary(x => x.Id);

                foreach (int selectedTargetActivityId in selectedTargetActivities)
                {
                    if (targetActivityLookup.TryGetValue(selectedTargetActivityId, out ISelectableActivityViewModel? vm))
                    {
                        m_SelectedTargetActivities.Add(vm);
                    }
                }
            }
            RaiseTargetActivitiesPropertiesChanged();
        }

        public void RaiseTargetActivitiesPropertiesChanged()
        {
            RefreshDerivedProperties();
            this.RaisePropertyChanged(nameof(TargetActivities));
            this.RaisePropertyChanged(nameof(TargetActivitiesString));
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return TargetActivitiesString;
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
                m_ReviseActivitiesSub?.Dispose();
                m_ShowConnectionsSub?.Dispose();
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
