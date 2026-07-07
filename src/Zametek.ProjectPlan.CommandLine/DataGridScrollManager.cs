using Zametek.Contract.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    public class DataGridScrollManager
        : IDataGridScrollManager
    {
        #region IDataGridScrollManager Members

        public object? GetScrollItem(string name)
        {
            return null;
        }

        public void SetScrollItem(string name, object? item)
        {
        }

        public void ClearScrollItems()
        {
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
