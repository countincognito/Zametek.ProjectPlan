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
        private readonly ConcurrentDictionary<string, object> m_ScrollItems;
        private readonly ISettingService m_SettingService;

        public DataGridManager(ISettingService settingService)
        {
            ArgumentNullException.ThrowIfNull(settingService);
            ResetActions = [];
            m_DataGridModels = [];
            m_ScrollItems = [];
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

        // Scroll positions are kept in memory only (never flushed to settings) so they
        // persist between tab changes within a session but reset whenever a project or
        // project scenario is loaded or reset (see ClearScrollItems).

        public object? GetScrollItem(string name)
        {
            if (m_ScrollItems.TryGetValue(name, out object? item))
            {
                return item;
            }
            return null;
        }

        public void SetScrollItem(string name, object? item)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (item is null)
            {
                m_ScrollItems.TryRemove(name, out _);
            }
            else
            {
                m_ScrollItems[name] = item;
            }
        }

        public void ClearScrollItems()
        {
            m_ScrollItems.Clear();
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
                m_ScrollItems.Clear();
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
