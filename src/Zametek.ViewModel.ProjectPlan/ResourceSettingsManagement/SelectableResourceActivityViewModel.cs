using ReactiveUI;
using System.Globalization;
using System.Reactive.Linq;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class SelectableResourceActivityViewModel
        : ViewModelBase, ISelectableResourceActivityViewModel
    {
        #region Fields

        private readonly IResourceActivitySelectorViewModel m_ResourceActivitySelectorViewModel;
        private readonly IDisposable? m_ResourceActivitySelectorSub;

        #endregion

        #region Ctors

        public SelectableResourceActivityViewModel(
            int id,
            string name,
            bool isSelected,
            IResourceActivitySelectorViewModel resourceActivitySelectorViewModel)
        {
            ArgumentNullException.ThrowIfNull(resourceActivitySelectorViewModel);
            Id = id;
            m_Name = name;
            m_IsSelected = isSelected;
            m_ResourceActivitySelectorViewModel = resourceActivitySelectorViewModel;

            m_ResourceActivitySelectorSub = this
                .ObservableForProperty(x => x.IsSelected)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => m_ResourceActivitySelectorViewModel.RaiseTargetResourceActivitiesPropertiesChanged());
        }

        #endregion

        #region ISelectableResourceActivityViewModel Members

        public int Id
        {
            get;
        }

        private string m_Name;
        public string Name
        {
            get => m_Name;
            set
            {
                this.RaiseAndSetIfChanged(ref m_Name, value);
                this.RaisePropertyChanged(nameof(DisplayName));
            }
        }

        public string DisplayName
        {
            get
            {
                return string.IsNullOrWhiteSpace(Name) ? Id.ToString(CultureInfo.InvariantCulture) : Name;
            }
        }

        private bool m_IsSelected;
        public bool IsSelected
        {
            get => m_IsSelected;
            set => this.RaiseAndSetIfChanged(ref m_IsSelected, value);
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
                m_ResourceActivitySelectorSub?.Dispose();
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
