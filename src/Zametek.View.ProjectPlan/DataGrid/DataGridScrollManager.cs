using System;
using System.Collections.Concurrent;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class DataGridScrollManager
        : IDataGridScrollManager
    {
        private readonly ConcurrentDictionary<string, object> m_ScrollItems;

        public DataGridScrollManager()
        {
            m_ScrollItems = [];
        }

        #region IDataGridScrollManager Members

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
