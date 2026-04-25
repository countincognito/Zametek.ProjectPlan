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
        private SortMode m_ProjectScenarioSortMode;
        private SortDirection m_ProjectScenarioSortDirection;
        private bool m_ScenarioChartShowNames;
        private TrackedMetrics m_ScenarioChartTrackedMetricXAxis;
        private TrackedMetrics m_ScenarioChartTrackedMetricYAxis;
        private CurveFittingType m_ScenarioChartCurveFittingType;
        private string m_SelectedTheme;

        #endregion

        #region Ctors

        public SettingService()
            : base(string.Empty)
        {
            m_Lock = new();
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

        public override string Layout
        {
            get
            {
                return string.Empty;
            }
            set
            {
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

        public override SortMode ProjectScenarioSortMode
        {
            get
            {
                return m_ProjectScenarioSortMode;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ProjectScenarioSortMode = value;
                }
            }
        }

        public override SortDirection ProjectScenarioSortDirection
        {
            get
            {
                return m_ProjectScenarioSortDirection;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ProjectScenarioSortDirection = value;
                }
            }
        }

        public override bool ScenarioChartShowNames
        {
            get => m_ScenarioChartShowNames;
            set
            {
                lock (m_Lock)
                {
                    m_ScenarioChartShowNames = value;
                }
            }
        }

        public override TrackedMetrics ScenarioChartTrackedMetricXAxis
        {
            get => m_ScenarioChartTrackedMetricXAxis;
            set
            {
                lock (m_Lock)
                {
                    m_ScenarioChartTrackedMetricXAxis = value;
                }
            }
        }

        public override TrackedMetrics ScenarioChartTrackedMetricYAxis
        {
            get => m_ScenarioChartTrackedMetricYAxis;
            set
            {
                lock (m_Lock)
                {
                    m_ScenarioChartTrackedMetricYAxis = value;
                }
            }
        }

        public override CurveFittingType ScenarioChartCurveFittingType
        {
            get => m_ScenarioChartCurveFittingType;
            set
            {
                lock (m_Lock)
                {
                    m_ScenarioChartCurveFittingType = value;
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
