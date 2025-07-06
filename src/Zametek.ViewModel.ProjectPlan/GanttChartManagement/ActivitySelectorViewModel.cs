using ReactiveUI;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ActivitySelectorViewModel
        : ViewModelBase, IActivitySelectorViewModel
    {
        #region Fields

        private readonly object m_Lock;
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

        #endregion

        #region Ctors

        public ActivitySelectorViewModel(
            ICoreViewModel coreViewModel,
            IList<TargetActivityModel> targetActivities)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            m_TargetActivities = new(s_EqualityComparer);
            m_ReadOnlyTargetActivities = new(m_TargetActivities);
            m_SelectedTargetActivities = new(s_EqualityComparer);

            m_SelectedTargetActivities.CollectionChanged += SelectedTargetActivities_CollectionChanged;

            // Initial set up.
            ReviseActivities(targetActivities);

            // This needs to be on the current thread because all the tracker updates
            // need to be completed before a compilation can start.
            m_ReviseActivitiesSub = this
                .WhenAnyValue(x => x.m_CoreViewModel.IsReadyToReviseTrackers)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(isReadyToRevise =>
                {
                    if (isReadyToRevise == ReadyToRevise.Yes)
                    {
                        ReviseActivities();
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

        public string TargetActivitiesString
        {
            get
            {
                lock (m_Lock)
                {
                    return string.Join(
                        DependenciesStringValidationRule.Separator,
                        SelectedTargetActivities.Select(x => x.DisplayName));
                }
            }
        }

        public IList<int> SelectedActivityIds
        {
            get
            {
                lock (m_Lock)
                {
                    return [.. SelectedTargetActivities.Select(x => x.Id)];
                }
            }
        }

        #endregion

        #region Private Members

        private void SelectedTargetActivities_CollectionChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            RaiseTargetActivitiesPropertiesChanged();
        }

        private void ReviseActivities(IList<TargetActivityModel> targetActivities)
        {
            lock (m_Lock)
            {
                HashSet<int> selectedActivityIds = targetActivities.Select(x => x.Id).ToHashSet();

                Dictionary<int, TargetActivityModel> activityLookup = targetActivities.ToDictionary(x => x.Id);

                List<TargetActivityModel> newActivities =
                    [.. m_CoreViewModel.Activities
                        .Select(activity => new TargetActivityModel
                        {
                            Id = activity.Id,
                            Name = activity.Name
                        })];

                foreach (TargetActivityModel activity in newActivities)
                {
                    activityLookup.TryAdd(activity.Id, activity);
                }

                SetTargetActivities(
                    activityLookup.Values,
                    selectedActivityIds);
            }
        }

        private void ReviseActivities()
        {
            lock (m_Lock)
            {
                List<TargetActivityModel> newActivities =
                    [.. m_CoreViewModel.Activities
                        .Select(activity => new TargetActivityModel
                        {
                            Id = activity.Id,
                            Name = activity.Name
                        })];

                SetTargetActivities(
                    newActivities,
                    [.. SelectedActivityIds]);
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
                // TODO: dispose managed state (managed objects).
                m_ReviseActivitiesSub?.Dispose();
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
