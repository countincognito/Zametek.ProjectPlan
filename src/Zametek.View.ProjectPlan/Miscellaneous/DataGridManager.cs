using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class DataGridManager
        : IDataGridManager
    {
        private readonly ConcurrentDictionary<string, DataGridModel> m_DataGridModels;
        private readonly ISettingService m_SettingService;

        public DataGridManager(ISettingService settingService)
        {
            ArgumentNullException.ThrowIfNull(settingService);
            ResetActions = [];
            m_DataGridModels = [];
            m_SettingService = settingService;

            Initialize();
        }

        #region Private Members

        private void Initialize()
        {
            IList<DataGridModel> dataGridModels = m_SettingService.GetDataGridLayout();

            foreach (DataGridModel dataGridModel in dataGridModels)
            {
                SetDataGridModel(dataGridModel);
            }
        }

        #endregion

        #region IDataGridManager Members

        public IList<Action> ResetActions { get; }

        public DataGridModel GetDataGridModel(string name)
        {
            if (m_DataGridModels.TryGetValue(name, out DataGridModel? dataGridModel))
            {
                return dataGridModel;
            }
            return new();
        }

        public void SetDataGridModel(DataGridModel dataGridModel)
        {
            m_DataGridModels[dataGridModel.Name] = dataGridModel;
        }

        public void SaveDataGridModels()
        {
            List<DataGridModel> dataGridModels = [.. m_DataGridModels.Values];
            m_SettingService.SetDataGridLayout(dataGridModels);
        }

        public void ResetDataGridModels()
        {
            foreach (Action action in ResetActions)
            {
                action();
            }
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
