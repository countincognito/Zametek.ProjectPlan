using ReactiveUI;
using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ResourceSelectorViewModel
        : ViewModelBase, IDisposable
    {
        #region Fields

        private readonly object m_Lock;
        private static readonly EqualityComparer<SelectableResourceViewModel> s_EqualityComparer =
            EqualityComparer<SelectableResourceViewModel>.Create(
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

        #endregion

        #region Ctors

        public ResourceSelectorViewModel()
        {
            m_Lock = new object();
            m_TargetResources = new(s_EqualityComparer);
            m_ReadOnlyTargetResources = new(m_TargetResources);
            m_SelectedTargetResources = new(s_EqualityComparer);
        }

        #endregion

        #region Properties

        private readonly ObservableUniqueCollection<SelectableResourceViewModel> m_TargetResources;
        private readonly ReadOnlyObservableCollection<SelectableResourceViewModel> m_ReadOnlyTargetResources;
        public ReadOnlyObservableCollection<SelectableResourceViewModel> TargetResources => m_ReadOnlyTargetResources;

        // Use ObservableUniqueCollection to prevent selected
        // items appearing twice in the Urse MultiComboBox.
        private readonly ObservableUniqueCollection<SelectableResourceViewModel> m_SelectedTargetResources;
        public ObservableCollection<SelectableResourceViewModel> SelectedTargetResources => m_SelectedTargetResources;

        public string TargetResourcesString
        {
            get
            {
                lock (m_Lock)
                {
                    return string.Join(
                        DependenciesStringValidationRule.Separator,
                        TargetResources.Where(x => x.IsSelected).Select(x => x.DisplayName));
                }
            }
        }

        public IList<int> SelectedResourceIds
        {
            get
            {
                lock (m_Lock)
                {
                    return TargetResources
                        .Where(x => x.IsSelected)
                        .Select(x => x.Id)
                        .ToList();
                }
            }
        }

        #endregion

        #region Public Methods

        public string GetAllocatedToResourcesString(HashSet<int> allocatedToResources)
        {
            ArgumentNullException.ThrowIfNull(allocatedToResources);
            lock (m_Lock)
            {
                return string.Join(
                    DependenciesStringValidationRule.Separator,
                    TargetResources.Where(x => allocatedToResources.Contains(x.Id))
                        .OrderBy(x => x.Id)
                        .Select(x => x.DisplayName));
            }
        }

        public void SetTargetResources(
            IEnumerable<ResourceModel> targetResources,
            HashSet<int> selectedTargetResources)
        {
            ArgumentNullException.ThrowIfNull(targetResources);
            ArgumentNullException.ThrowIfNull(selectedTargetResources);
            lock (m_Lock)
            {
                {
                    // Find target view models that have been removed.
                    List<SelectableResourceViewModel> removedViewModels = m_TargetResources
                        .ExceptBy(targetResources.Select(x => x.Id), x => x.Id)
                        .ToList();

                    // Delete the removed items from the target and selected collections.
                    foreach (SelectableResourceViewModel vm in removedViewModels)
                    {
                        m_TargetResources.Remove(vm);
                        m_SelectedTargetResources.Remove(vm);
                        vm.Dispose();
                    }

                    // Find the selected view models that have been removed.
                    List<SelectableResourceViewModel> removedSelectedViewModels = m_SelectedTargetResources
                        .ExceptBy(selectedTargetResources, x => x.Id)
                        .ToList();

                    // Delete the removed selected items from the selected collections.
                    foreach (SelectableResourceViewModel vm in removedSelectedViewModels)
                    {
                        vm.IsSelected = false;
                        m_SelectedTargetResources.Remove(vm);
                    }
                }
                {
                    // Find the target models that have been added.
                    List<ResourceModel> addedModels = targetResources
                        .ExceptBy(m_TargetResources.Select(x => x.Id), x => x.Id)
                        .ToList();

                    List<SelectableResourceViewModel> addedViewModels = [];

                    // Create a collection of new view models.
                    foreach (ResourceModel model in addedModels)
                    {
                        var vm = new SelectableResourceViewModel(
                            model.Id,
                            model.Name,
                            selectedTargetResources.Contains(model.Id),
                            this);

                        m_TargetResources.Add(vm);
                        if (vm.IsSelected)
                        {
                            m_SelectedTargetResources.Add(vm);
                        }
                    }
                }
                {
                    // Update names.
                    Dictionary<int, ResourceModel> targetResourceLookup = targetResources.ToDictionary(x => x.Id);

                    foreach (SelectableResourceViewModel vm in m_TargetResources)
                    {
                        if (targetResourceLookup.TryGetValue(vm.Id, out ResourceModel? value))
                        {
                            vm.Name = value.Name;
                        }
                    }
                }
            }
            RaiseTargetResourcesPropertiesChanged();
        }

        public void ClearTargetResources()
        {
            lock (m_Lock)
            {
                foreach (IDisposable targetResource in TargetResources)
                {
                    targetResource.Dispose();
                }
                m_TargetResources.Clear();
            }
        }

        public void ClearSelectedTargetResources()
        {
            lock (m_Lock)
            {
                foreach (IDisposable targetResource in SelectedTargetResources)
                {
                    targetResource.Dispose();
                }
                m_SelectedTargetResources.Clear();
            }
        }

        public void RaiseTargetResourcesPropertiesChanged()
        {
            this.RaisePropertyChanged(nameof(TargetResources));
            this.RaisePropertyChanged(nameof(TargetResourcesString));
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return TargetResourcesString;
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
                ClearTargetResources();
                ClearSelectedTargetResources();
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
