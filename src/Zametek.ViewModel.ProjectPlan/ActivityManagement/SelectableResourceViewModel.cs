using ReactiveUI;
using System.Globalization;
using System.Reactive.Linq;

namespace Zametek.ViewModel.ProjectPlan
{
    public class SelectableResourceViewModel
        : ViewModelBase, IDisposable
    {
        #region Fields

        private readonly ResourceSelectorViewModel m_ResourceSelectorViewModel;
        private readonly IDisposable? m_ResourceSelectorSub;

        #endregion

        #region Ctors

        public SelectableResourceViewModel(
            int id,
            string name,//!!,
            bool isSelected,
            ResourceSelectorViewModel resourceSelectorViewModel)
        {
            ArgumentNullException.ThrowIfNull(resourceSelectorViewModel);
            Id = id;
            m_Name = name;
            IsSelected = isSelected;
            m_ResourceSelectorViewModel = resourceSelectorViewModel;

            m_DisplayName = this
                .WhenAnyValue(x => x.Name)
                .Select(x => string.IsNullOrWhiteSpace(x) ? Id.ToString(CultureInfo.InvariantCulture) : x)
                .ToProperty(this, x => x.DisplayName);

            m_ResourceSelectorSub = this
                .WhenAnyValue(x => x.IsSelected)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => m_ResourceSelectorViewModel.RaiseTargetResourcesPropertiesChanged());
        }

        #endregion

        #region Properties

        public int Id
        {
            get;
        }

        private string m_Name;
        public string Name
        {
            get => m_Name;
            set => this.RaiseAndSetIfChanged(ref m_Name, value);
        }

        private readonly ObservableAsPropertyHelper<string> m_DisplayName;
        public string DisplayName => m_DisplayName.Value;

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
                m_ResourceSelectorSub?.Dispose();
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
