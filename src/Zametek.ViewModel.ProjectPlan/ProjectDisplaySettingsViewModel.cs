using ReactiveUI;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectDisplaySettingsViewModel
        : ViewModelBase, IProjectDisplaySettingsViewModel
    {
        #region Fields

        private readonly Lock m_Lock;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private Action<bool>? m_SetIsProjectUpdated;

        #endregion

        #region Ctors

        public ProjectDisplaySettingsViewModel(
            IDateTimeCalculator dateTimeCalculator,
            Action<bool> setIsProjectUpdated)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(setIsProjectUpdated);
            m_Lock = new();
            m_DateTimeCalculator = dateTimeCalculator;
            m_SetIsProjectUpdated = setIsProjectUpdated;
        }

        #endregion

        #region Private Members

        private void SetIsProjectUpdated(bool isProjectUpdated)
        {
            lock (m_Lock)
            {
                if (m_SetIsProjectUpdated is not null)
                {
                    m_SetIsProjectUpdated(isProjectUpdated);
                }
            }
        }

        #endregion

        #region IProjectDisplaySettingsViewModel Members

        private SortMode m_ProjectScenarioSortMode;
        public SortMode ProjectScenarioSortMode
        {
            get => m_ProjectScenarioSortMode;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ProjectScenarioSortMode, value);
                }
            }
        }

        private SortDirection m_ProjectScenarioSortDirection;
        public SortDirection ProjectScenarioSortDirection
        {
            get
            {
                return m_ProjectScenarioSortDirection;
            }
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ProjectScenarioSortDirection, value);
                }
            }
        }

        private bool m_ScenarioChartShowNames;
        public bool ScenarioChartShowNames
        {
            get => m_ScenarioChartShowNames;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartShowNames, value);
                }
            }
        }

        private TrackedMetrics m_ScenarioChartTrackedMetricXAxis;
        public TrackedMetrics ScenarioChartTrackedMetricXAxis
        {
            get => m_ScenarioChartTrackedMetricXAxis;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartTrackedMetricXAxis, value);
                }
            }
        }

        private TrackedMetrics m_ScenarioChartTrackedMetricYAxis;
        public TrackedMetrics ScenarioChartTrackedMetricYAxis
        {
            get => m_ScenarioChartTrackedMetricYAxis;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartTrackedMetricYAxis, value);
                }
            }
        }

        private CurveFittingType m_ScenarioChartCurveFittingType;
        public CurveFittingType ScenarioChartCurveFittingType
        {
            get => m_ScenarioChartCurveFittingType;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartCurveFittingType, value);
                }
            }
        }

        public void SetValues(ProjectDisplaySettingsModel model)
        {
            lock (m_Lock)
            {
                if (ProjectScenarioSortMode != model.ProjectScenarioSortMode)
                {
                    ProjectScenarioSortMode = model.ProjectScenarioSortMode;
                }
                if (ProjectScenarioSortDirection != model.ProjectScenarioSortDirection)
                {
                    ProjectScenarioSortDirection = model.ProjectScenarioSortDirection;
                }


                if (ScenarioChartShowNames != model.ScenarioChartShowNames)
                {
                    ScenarioChartShowNames = model.ScenarioChartShowNames;
                }
                if (ScenarioChartTrackedMetricXAxis != model.ScenarioChartTrackedMetricXAxis)
                {
                    ScenarioChartTrackedMetricXAxis = model.ScenarioChartTrackedMetricXAxis;
                }
                if (ScenarioChartTrackedMetricYAxis != model.ScenarioChartTrackedMetricYAxis)
                {
                    ScenarioChartTrackedMetricYAxis = model.ScenarioChartTrackedMetricYAxis;
                }
                if (ScenarioChartCurveFittingType != model.ScenarioChartCurveFittingType)
                {
                    ScenarioChartCurveFittingType = model.ScenarioChartCurveFittingType;
                }
            }
        }

        public ProjectDisplaySettingsModel GetValues()
        {
            lock (m_Lock)
            {
                return new ProjectDisplaySettingsModel
                {
                    ProjectScenarioSortMode = ProjectScenarioSortMode,
                    ProjectScenarioSortDirection = ProjectScenarioSortDirection,

                    ScenarioChartShowNames = ScenarioChartShowNames,
                    ScenarioChartTrackedMetricXAxis = ScenarioChartTrackedMetricXAxis,
                    ScenarioChartTrackedMetricYAxis = ScenarioChartTrackedMetricYAxis,
                    ScenarioChartCurveFittingType = ScenarioChartCurveFittingType,
                };
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
                // TODO: dispose managed state (managed objects).
                m_SetIsProjectUpdated = null;
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

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
