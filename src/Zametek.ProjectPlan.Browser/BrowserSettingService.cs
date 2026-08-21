using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Zametek.Common.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.Browser
{
    /// <summary>
    /// Settings held for the lifetime of the page.
    /// </summary>
    /// <remarks>
    /// The base class reads its settings file in the constructor, so it is handed an empty filename:
    /// File.Exists("") is false, which is what keeps a browser build away from the file system.
    /// <para>
    /// Everything is stored faithfully rather than discarded, so the dock layout, grid layouts and
    /// recents all behave correctly within a session. They simply do not outlive a page reload yet -
    /// making them durable means backing this with localStorage or IndexedDB, which is a separate
    /// piece of work, and confining it to this class is the point of the arrangement.
    /// </para>
    /// </remarks>
    public class BrowserSettingService
        : SettingServiceBase
    {
        #region Fields

        private readonly Lock m_Lock;
        private readonly List<DataGridModel> m_DataGridLayouts;
        private readonly List<string> m_RecentProjectFilePaths;
        private string m_DockLayout;
        private string m_ProjectDirectory;
        private bool m_DefaultShowDates;
        private bool m_DefaultUseClassicDates;
        private NonWorkingDayMode m_DefaultNonWorkingDayMode;
        private bool m_DefaultHideCost;
        private bool m_DefaultHideBilling;
        private string m_SelectedTheme;
        private int m_CompilationTimeoutMilliseconds;

        #endregion

        #region Ctors

        public BrowserSettingService()
            : base(string.Empty)
        {
            m_Lock = new();
            m_DataGridLayouts = [];
            m_RecentProjectFilePaths = [];
            m_DockLayout = string.Empty;
            m_ProjectDirectory = string.Empty;
            m_SelectedTheme = string.Empty;
            m_CompilationTimeoutMilliseconds = AppSettingsModel.DefaultCompilationTimeoutMilliseconds;
        }

        #endregion

        #region ISettingService Members

        // There is no ambient working directory in a browser: files arrive as handles the user has
        // picked, not as paths resolved against a folder. Reporting empty keeps callers from building
        // a path out of it, and the file dialogs ignore the suggested start location anyway.
        public override string ProjectDirectory
        {
            get => m_ProjectDirectory;
            protected set
            {
                lock (m_Lock)
                {
                    m_ProjectDirectory = value;
                }
            }
        }

        public override string DockLayout
        {
            get => m_DockLayout;
            set
            {
                lock (m_Lock)
                {
                    m_DockLayout = value;
                }
            }
        }

        public override IList<DataGridModel> GetDataGridLayout()
        {
            lock (m_Lock)
            {
                return [.. m_DataGridLayouts];
            }
        }

        public override void SetDataGridLayout(IList<DataGridModel> models)
        {
            ArgumentNullException.ThrowIfNull(models);

            lock (m_Lock)
            {
                m_DataGridLayouts.Clear();
                m_DataGridLayouts.AddRange(models);
            }
        }

        public override bool DefaultShowDates
        {
            get => m_DefaultShowDates;
            set
            {
                lock (m_Lock)
                {
                    m_DefaultShowDates = value;
                }
            }
        }

        public override bool DefaultUseClassicDates
        {
            get => m_DefaultUseClassicDates;
            set
            {
                lock (m_Lock)
                {
                    m_DefaultUseClassicDates = value;
                }
            }
        }

        public override NonWorkingDayMode DefaultNonWorkingDayMode
        {
            get => m_DefaultNonWorkingDayMode;
            set
            {
                lock (m_Lock)
                {
                    m_DefaultNonWorkingDayMode = value;
                }
            }
        }

        public override bool DefaultHideCost
        {
            get => m_DefaultHideCost;
            set
            {
                lock (m_Lock)
                {
                    m_DefaultHideCost = value;
                }
            }
        }

        public override bool DefaultHideBilling
        {
            get => m_DefaultHideBilling;
            set
            {
                lock (m_Lock)
                {
                    m_DefaultHideBilling = value;
                }
            }
        }

        public override string SelectedTheme
        {
            get => m_SelectedTheme;
            set
            {
                lock (m_Lock)
                {
                    m_SelectedTheme = value;
                }
            }
        }

        public override int CompilationTimeoutMilliseconds
        {
            get => m_CompilationTimeoutMilliseconds;
            set
            {
                lock (m_Lock)
                {
                    m_CompilationTimeoutMilliseconds = value;
                }
            }
        }

        public override int MaxRecentProjectFilePaths => m_AppSettingsModel.MaxRecentProjectFilePaths;

        public override IReadOnlyList<string> RecentProjectFilePaths
        {
            get
            {
                lock (m_Lock)
                {
                    return [.. m_RecentProjectFilePaths];
                }
            }
        }

        public override void RecordRecentProjectFilePath(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return;
            }

            lock (m_Lock)
            {
                // Most recent first, no duplicates, oldest dropped once the list is full - the same
                // contract the desktop service keeps.
                m_RecentProjectFilePaths.RemoveAll(x => string.Equals(x, filename, StringComparison.OrdinalIgnoreCase));
                m_RecentProjectFilePaths.Insert(0, filename);

                while (m_RecentProjectFilePaths.Count > MaxRecentProjectFilePaths)
                {
                    m_RecentProjectFilePaths.RemoveAt(m_RecentProjectFilePaths.Count - 1);
                }
            }
        }

        public override void RemoveRecentProjectFilePath(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return;
            }

            lock (m_Lock)
            {
                m_RecentProjectFilePaths.RemoveAll(x => string.Equals(x, filename, StringComparison.OrdinalIgnoreCase));
            }
        }

        public override void ClearRecentProjectFilePaths()
        {
            lock (m_Lock)
            {
                m_RecentProjectFilePaths.Clear();
            }
        }

        #endregion
    }
}
