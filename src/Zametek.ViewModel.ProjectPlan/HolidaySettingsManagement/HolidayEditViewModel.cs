using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class HolidayEditViewModel
        : ViewModelBase, IHolidayEditViewModel
    {
        #region Ctors

        public HolidayEditViewModel()
        {
        }

        #endregion

        #region Private Members

        #endregion

        #region IHolidayEditViewModel Members

        public UpdateHolidayModel BuildUpdateModel()
        {
            var updateModel = new UpdateHolidayModel
            {
                Name = string.Empty,
                IsNameEdited = false,

                Notes = string.Empty,
                IsNotesEdited = false,

                RecurrencePattern = string.Empty,
                IsRecurrencePatternEdited = false,
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
