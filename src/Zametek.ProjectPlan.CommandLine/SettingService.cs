using Zametek.Common.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    public class SettingService
        : SettingServiceBase
    {
        #region Fields

        private readonly Lock m_Lock;
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

        public SettingService()
            : base(string.Empty)
        {
            m_Lock = new();
            m_ProjectDirectory = string.Empty;
            m_SelectedTheme = string.Empty;

            // This host never reads the desktop settings file, so the default comes
            // from the model rather than from disk. --compile-timeout overwrites it.
            m_CompilationTimeoutMilliseconds = AppSettingsModel.DefaultCompilationTimeoutMilliseconds;
        }

        #endregion

        #region ISettingService Members

        public override string ProjectDirectory
        {
            get
            {
                string directory = m_ProjectDirectory;
                return string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : directory;
            }
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
            get
            {
                return string.Empty;
            }
            set
            {
            }
        }

        // The command line tool has no data grids, so layout persistence is
        // inert: nothing is stored and recording is a no-op.

        public override IList<DataGridModel> GetDataGridLayout()
        {
            return [];
        }

        public override void SetDataGridLayout(IList<DataGridModel> models)
        {
        }

        public override bool DefaultShowDates
        {
            get
            {
                return m_DefaultShowDates;
            }
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
            get
            {
                return m_DefaultUseClassicDates;
            }
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
            get
            {
                return m_DefaultNonWorkingDayMode;
            }
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
            get
            {
                return m_DefaultHideCost;
            }
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
            get
            {
                return m_DefaultHideBilling;
            }
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
            get
            {
                return m_SelectedTheme;
            }
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
            get
            {
                return m_CompilationTimeoutMilliseconds;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CompilationTimeoutMilliseconds = value;
                }
            }
        }

        // The command line tool has no recently opened file menu, so the recents
        // are inert: nothing is stored and recording is a no-op.

        public override int MaxRecentProjectFilePaths => 0;

        public override IReadOnlyList<string> RecentProjectFilePaths => [];

        public override void RecordRecentProjectFilePath(string filename)
        {
        }

        public override void RemoveRecentProjectFilePath(string filename)
        {
        }

        public override void ClearRecentProjectFilePaths()
        {
        }

        #endregion
    }
}
