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

        #endregion

        #region Ctors

        public ResourceSelectorViewModel()
        {
            m_Lock = new object();
            m_TargetResources = [];
            m_ReadOnlyTargetResources = new ReadOnlyObservableCollection<SelectableResourceViewModel>(m_TargetResources);
            m_SelectedTargetResources = [];
        }

        #endregion

        #region Properties

        private readonly ObservableCollection<SelectableResourceViewModel> m_TargetResources;
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
                m_TargetResources.Clear();
                m_SelectedTargetResources.Clear();
                foreach (ResourceModel targetResource in targetResources)
                {
                    var vm = new SelectableResourceViewModel(
                        targetResource.Id,
                        targetResource.Name,
                        selectedTargetResources.Contains(targetResource.Id),
                        this);

                    m_TargetResources.Add(vm);
                    if (vm.IsSelected)
                    {
                        m_SelectedTargetResources.Add(vm);
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
