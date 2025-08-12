using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    public class SettingService
        : SettingServiceBase
    {
        #region Fields

        private readonly object m_Lock;
        private string m_ProjectDirectory;
        private bool m_DefaultShowDates;
        private bool m_DefaultUseClassicDates;
        private bool m_DefaultUseBusinessDays;
        private bool m_DefaultHideCost;
        private bool m_DefaultHideBilling;
        private string m_SelectedTheme;

        #endregion

        #region Ctors

        public SettingService()
            : base(string.Empty)
        {
            m_Lock = new object();
            m_ProjectDirectory = string.Empty;
            m_SelectedTheme = string.Empty;
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

        public override bool DefaultUseBusinessDays
        {
            get
            {
                return m_DefaultUseBusinessDays;
            }
            set
            {
                lock (m_Lock)
                {
                    m_DefaultUseBusinessDays = value;
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

        #endregion
    }
}
