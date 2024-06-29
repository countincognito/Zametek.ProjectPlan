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

        #endregion

        #region Ctors

        public WorkStreamSelectorViewModel()
        {
            m_Lock = new object();
            m_TargetWorkStreams = [];
            m_ReadOnlyTargetWorkStreams = new ReadOnlyObservableCollection<SelectableWorkStreamViewModel>(m_TargetWorkStreams);
        }

        #endregion

        #region Properties

        private readonly ObservableCollection<SelectableWorkStreamViewModel> m_TargetWorkStreams;
        private readonly ReadOnlyObservableCollection<SelectableWorkStreamViewModel> m_ReadOnlyTargetWorkStreams;
        public ReadOnlyObservableCollection<SelectableWorkStreamViewModel> TargetWorkStreams => m_ReadOnlyTargetWorkStreams;

        public string TargetWorkStreamsString
        {
            get
            {
                lock (m_Lock)
                {
                    return string.Join(
                        DependenciesStringValidationRule.Separator,
                        TargetWorkStreams.Where(x => x.IsSelected).Select(x => x.DisplayName));
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
                        .Where(x => x.IsSelected)
                        .Select(x => x.Id)
                        .ToList();
                }
            }
        }

        #endregion

        #region Public Methods

        //public string GetAllocatedToResourcesString(HashSet<int> allocatedToResources)
        //{
        //    ArgumentNullException.ThrowIfNull(allocatedToResources);
        //    lock (m_Lock)
        //    {
        //        return string.Join(
        //            DependenciesStringValidationRule.Separator,
        //            TargetResources.Where(x => allocatedToResources.Contains(x.Id))
        //                .OrderBy(x => x.Id)
        //                .Select(x => x.DisplayName));
        //    }
        //}

        public void SetTargetWorkStreams(
            IEnumerable<WorkStreamModel> targetWorkStreams,
            HashSet<int> selectedTargetWorkStreams)
        {
            ArgumentNullException.ThrowIfNull(targetWorkStreams);
            ArgumentNullException.ThrowIfNull(selectedTargetWorkStreams);
            lock (m_Lock)
            {
                m_TargetWorkStreams.Clear();
                foreach (WorkStreamModel targetWorkStream in targetWorkStreams)
                {
                    m_TargetWorkStreams.Add(
                        new SelectableWorkStreamViewModel(
                            targetWorkStream.Id,
                            targetWorkStream.Name,
                            selectedTargetWorkStreams.Contains(targetWorkStream.Id),
                            this));
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
