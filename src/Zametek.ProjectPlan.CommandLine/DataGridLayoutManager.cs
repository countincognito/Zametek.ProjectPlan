using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    public class DataGridLayoutManager
        : IDataGridLayoutManager
    {
        public DataGridLayoutManager(ISettingService settingService)
        {
            ArgumentNullException.ThrowIfNull(settingService);
            ResetActions = [];
        }

        #region IDataGridLayoutManager Members

        public IList<Action> ResetActions { get; }

        public DataGridModel GetDataGridModel(string name)
        {
            return new();
        }

        public void SetDataGridModel(DataGridModel dataGridModel)
        {
        }

        public void SaveDataGridModels()
        {
        }

        public void ResetDataGridModels()
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

            if (disposing)
            {
                ResetActions.Clear();
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
