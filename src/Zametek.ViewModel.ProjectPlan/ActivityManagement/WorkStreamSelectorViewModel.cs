using ReactiveUI;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class WorkStreamSelectorViewModel
        : ViewModelBase, IWorkStreamSelectorViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private readonly bool m_PhaseOnly;
        private static readonly EqualityComparer<ISelectableWorkStreamViewModel> s_EqualityComparer =
            EqualityComparer<ISelectableWorkStreamViewModel>.Create(
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

        private static readonly Comparer<ISelectableWorkStreamViewModel> s_SortComparer =
            Comparer<ISelectableWorkStreamViewModel>.Create(
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

        #endregion

        #region Ctors

        public WorkStreamSelectorViewModel()
            : this(false)
        {
        }

        public WorkStreamSelectorViewModel(bool phaseOnly)
        {
            m_Lock = new object();
            m_PhaseOnly = phaseOnly;
            m_TargetWorkStreams = new(s_EqualityComparer);
            m_ReadOnlyTargetWorkStreams = new(m_TargetWorkStreams);
            m_SelectedTargetWorkStreams = new(s_EqualityComparer);

            m_SelectedTargetWorkStreams.CollectionChanged += SelectedTargetWorkStreams_CollectionChanged;
        }

        #endregion

        #region Properties

        private readonly ObservableUniqueCollection<ISelectableWorkStreamViewModel> m_TargetWorkStreams;
        private readonly ReadOnlyObservableCollection<ISelectableWorkStreamViewModel> m_ReadOnlyTargetWorkStreams;
        public ReadOnlyObservableCollection<ISelectableWorkStreamViewModel> TargetWorkStreams => m_ReadOnlyTargetWorkStreams;

        // Use ObservableUniqueCollection to prevent selected
        // items appearing twice in the Urse MultiComboBox.
        private readonly ObservableUniqueCollection<ISelectableWorkStreamViewModel> m_SelectedTargetWorkStreams;
        public ObservableCollection<ISelectableWorkStreamViewModel> SelectedTargetWorkStreams => m_SelectedTargetWorkStreams;

        public string TargetWorkStreamsString
        {
            get
            {
                lock (m_Lock)
                {
                    return string.Join(
                        DependenciesStringValidationRule.Separator,
                        SelectedTargetWorkStreams
                            .Where(x => (!m_PhaseOnly)
                                    || (m_PhaseOnly && x.IsPhase))
                            .Select(x => x.DisplayName));
                }
            }
        }

        public IList<int> SelectedWorkStreamIds
        {
            get
            {
                lock (m_Lock)
                {
                    return SelectedTargetWorkStreams
                        .Where(x => (!m_PhaseOnly)
                                || (m_PhaseOnly && x.IsPhase))
                        .Select(x => x.Id)
                        .ToList();
                }
            }
        }

        #endregion

        #region Private Members

        private void SelectedTargetWorkStreams_CollectionChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            RaiseTargetWorkStreamsPropertiesChanged();
        }

        #endregion

        #region Public Methods

        public void SetTargetWorkStreams(
            IEnumerable<TargetWorkStreamModel> targetWorkStreams,
            HashSet<int> selectedTargetWorkStreams)
        {
            ArgumentNullException.ThrowIfNull(targetWorkStreams);
            ArgumentNullException.ThrowIfNull(selectedTargetWorkStreams);
            lock (m_Lock)
            {
                IEnumerable<TargetWorkStreamModel> correctTargetWorkStreams =
                    targetWorkStreams.Where(x => (!m_PhaseOnly) || (m_PhaseOnly && x.IsPhase));

                {
                    // Find target view models that have been removed.
                    List<ISelectableWorkStreamViewModel> removedViewModels = m_TargetWorkStreams
                        .ExceptBy(correctTargetWorkStreams.Select(x => x.Id), x => x.Id)
                        .ToList();

                    // Delete the removed items from the target and selected collections.
                    foreach (ISelectableWorkStreamViewModel vm in removedViewModels)
                    {
                        m_TargetWorkStreams.Remove(vm);
                        m_SelectedTargetWorkStreams.Remove(vm);
                    }

                    // Find the selected view models that have been removed.
                    List<ISelectableWorkStreamViewModel> removedSelectedViewModels = m_SelectedTargetWorkStreams
                        .ExceptBy(selectedTargetWorkStreams, x => x.Id)
                        .ToList();

                    // Delete the removed selected items from the selected collections.
                    foreach (ISelectableWorkStreamViewModel vm in removedSelectedViewModels)
                    {
                        m_SelectedTargetWorkStreams.Remove(vm);
                    }
                }
                {
                    // Find the target models that have been added.
                    List<TargetWorkStreamModel> addedModels = correctTargetWorkStreams
                        .ExceptBy(m_TargetWorkStreams.Select(x => x.Id), x => x.Id)
                        .ToList();

                    List<ISelectableWorkStreamViewModel> addedViewModels = [];

                    // Create a collection of new view models.
                    foreach (TargetWorkStreamModel model in addedModels)
                    {
                        var vm = new SelectableWorkStreamViewModel(
                              model.Id,
                              model.Name,
                              model.IsPhase);

                        m_TargetWorkStreams.Add(vm);
                        if (selectedTargetWorkStreams.Contains(model.Id))
                        {
                            m_SelectedTargetWorkStreams.Add(vm);
                        }
                    }
                }
                {
                    // Update names.
                    Dictionary<int, TargetWorkStreamModel> targetWorkStreamLookup = correctTargetWorkStreams.ToDictionary(x => x.Id);

                    foreach (ISelectableWorkStreamViewModel vm in m_TargetWorkStreams)
                    {
                        if (targetWorkStreamLookup.TryGetValue(vm.Id, out TargetWorkStreamModel? value))
                        {
                            vm.Name = value.Name;
                        }
                    }
                }

                m_TargetWorkStreams.Sort(s_SortComparer);
            }
            RaiseTargetWorkStreamsPropertiesChanged();
        }

        public void RaiseTargetWorkStreamsPropertiesChanged()
        {
            this.RaisePropertyChanged(nameof(TargetWorkStreams));
            this.RaisePropertyChanged(nameof(TargetWorkStreamsString));
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return TargetWorkStreamsString;
        }

        #endregion
    }
}
