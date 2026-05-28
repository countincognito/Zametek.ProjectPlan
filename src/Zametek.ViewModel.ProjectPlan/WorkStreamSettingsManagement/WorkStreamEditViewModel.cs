using ReactiveUI;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class WorkStreamEditViewModel
        : ViewModelBase, IWorkStreamEditViewModel
    {
        #region Ctors

        public WorkStreamEditViewModel()
        {
            m_ColorFormat = ColorHelper.None();
        }

        #endregion

        #region IWorkStreamEditViewModel Members

        private bool m_IsPhase;
        public bool IsPhase
        {
            get => m_IsPhase;
            set
            {
                m_IsPhase = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsIsPhaseActive;
        public bool IsIsPhaseActive
        {
            get => m_IsIsPhaseActive;
            set
            {
                m_IsIsPhaseActive = value;
                this.RaisePropertyChanged();
            }
        }

        private ColorFormatModel m_ColorFormat;
        public ColorFormatModel ColorFormat
        {
            get => m_ColorFormat;
            set
            {
                m_ColorFormat = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsColorFormatActive;
        public bool IsColorFormatActive
        {
            get => m_IsColorFormatActive;
            set
            {
                m_IsColorFormatActive = value;
                this.RaisePropertyChanged();
            }
        }

        public UpdateWorkStreamModel BuildUpdateModel()
        {
            var updateModel = new UpdateWorkStreamModel
            {
                IsPhase = IsPhase,
                IsIsPhaseEdited = IsIsPhaseActive,

                ColorFormat = ColorFormat,
                IsColorFormatActive = IsColorFormatActive,
            };
            return updateModel;
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
                // Dispose managed state (managed objects).
                //m_IsBusy?.Dispose();
            }

            // Free unmanaged resources (unmanaged objects) and override a finalizer below.
            // Set large fields to null.

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
