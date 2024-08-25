using ReactiveUI;
using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class WorkStreamSelectorViewModel
        : ViewModelBase, IDisposable
    {
        #region Fields

        private readonly object m_Lock;
        private readonly bool m_PhaseOnly;

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
            m_TargetWorkStreams = [];
            m_ReadOnlyTargetWorkStreams = new ReadOnlyObservableCollection<SelectableWorkStreamViewModel>(m_TargetWorkStreams);
            m_SelectedTargetWorkStreams = [];
        }

        #endregion

        #region Properties

        private readonly ObservableCollection<SelectableWorkStreamViewModel> m_TargetWorkStreams;
        private readonly ReadOnlyObservableCollection<SelectableWorkStreamViewModel> m_ReadOnlyTargetWorkStreams;
        public ReadOnlyObservableCollection<SelectableWorkStreamViewModel> TargetWorkStreams => m_ReadOnlyTargetWorkStreams;

        // Use ObservableUniqueCollection to prevent selected
        // items appearing twice in the Urse MultiComboBox.
        private readonly ObservableUniqueCollection<SelectableWorkStreamViewModel> m_SelectedTargetWorkStreams;
        public ObservableCollection<SelectableWorkStreamViewModel> SelectedTargetWorkStreams => m_SelectedTargetWorkStreams;

        public string TargetWorkStreamsString
        {
            get
            {
                lock (m_Lock)
                {
                    return string.Join(
                        DependenciesStringValidationRule.Separator,
                        TargetWorkStreams
                            .Where(x => (!m_PhaseOnly && x.IsSelected)
                                    || (m_PhaseOnly && x.IsSelected && x.IsPhase))
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
                    return TargetWorkStreams
                        .Where(x => (!m_PhaseOnly && x.IsSelected)
                                || (m_PhaseOnly && x.IsSelected && x.IsPhase))
                        .Select(x => x.Id)
                        .ToList();
                }
            }
        }

        #endregion

        #region Public Methods

        public void SetTargetWorkStreams(
            IEnumerable<WorkStreamModel> targetWorkStreams,
            HashSet<int> selectedTargetWorkStreams)
        {
            ArgumentNullException.ThrowIfNull(targetWorkStreams);
            ArgumentNullException.ThrowIfNull(selectedTargetWorkStreams);
            lock (m_Lock)
            {
                m_TargetWorkStreams.Clear();
                m_SelectedTargetWorkStreams.Clear();
                foreach (WorkStreamModel targetWorkStream in targetWorkStreams.Where(x => (!m_PhaseOnly) || (m_PhaseOnly && x.IsPhase)))
                {
                    var vm = new SelectableWorkStreamViewModel(
                        targetWorkStream.Id,
                        targetWorkStream.Name,
                        targetWorkStream.IsPhase,
                        selectedTargetWorkStreams.Contains(targetWorkStream.Id),
                        this);

                    m_TargetWorkStreams.Add(vm);
                    if (vm.IsSelected)
                    {
                        m_SelectedTargetWorkStreams.Add(vm);
                    }
                }
            }
            RaiseTargetWorkStreamsPropertiesChanged();
        }

        public void ClearTargetWorkStreams()
        {
            lock (m_Lock)
            {
                foreach (IDisposable targetWorkStream in TargetWorkStreams)
                {
                    targetWorkStream.Dispose();
                }
                m_TargetWorkStreams.Clear();
            }
        }

        public void ClearSelectedTargetWorkStreams()
        {
            lock (m_Lock)
            {
                foreach (IDisposable targetWorkStream in SelectedTargetWorkStreams)
                {
                    targetWorkStream.Dispose();
                }
                m_SelectedTargetWorkStreams.Clear();
            }
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
                ClearTargetWorkStreams();
                ClearSelectedTargetWorkStreams();
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
