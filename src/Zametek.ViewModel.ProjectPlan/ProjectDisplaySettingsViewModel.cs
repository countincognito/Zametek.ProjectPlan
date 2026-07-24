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

        private TrackedMetrics m_ScenarioChartTrackedMetricY1Axis;
        public TrackedMetrics ScenarioChartTrackedMetricY1Axis
        {
            get => m_ScenarioChartTrackedMetricY1Axis;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartTrackedMetricY1Axis, value);
                }
            }
        }

        private TrackedMetrics m_ScenarioChartTrackedMetricY2Axis;
        public TrackedMetrics ScenarioChartTrackedMetricY2Axis
        {
            get => m_ScenarioChartTrackedMetricY2Axis;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartTrackedMetricY2Axis, value);
                }
            }
        }

        private CurveFittingType m_ScenarioChartCurveFittingTypeY1;
        public CurveFittingType ScenarioChartCurveFittingTypeY1
        {
            get => m_ScenarioChartCurveFittingTypeY1;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartCurveFittingTypeY1, value);
                }
            }
        }

        private CurveFittingType m_ScenarioChartCurveFittingTypeY2;
        public CurveFittingType ScenarioChartCurveFittingTypeY2
        {
            get => m_ScenarioChartCurveFittingTypeY2;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartCurveFittingTypeY2, value);
                }
            }
        }

        private bool m_ScenarioChartShowDerivativeY1;
        public bool ScenarioChartShowDerivativeY1
        {
            get => m_ScenarioChartShowDerivativeY1;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartShowDerivativeY1, value);
                }
            }
        }

        private bool m_ScenarioChartShowDerivativeY2;
        public bool ScenarioChartShowDerivativeY2
        {
            get => m_ScenarioChartShowDerivativeY2;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartShowDerivativeY2, value);
                }
            }
        }

        private bool m_ScenarioChartAbsoluteCurveFittingY1;
        public bool ScenarioChartAbsoluteCurveFittingY1
        {
            get => m_ScenarioChartAbsoluteCurveFittingY1;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartAbsoluteCurveFittingY1, value);
                }
            }
        }

        private bool m_ScenarioChartAbsoluteCurveFittingY2;
        public bool ScenarioChartAbsoluteCurveFittingY2
        {
            get => m_ScenarioChartAbsoluteCurveFittingY2;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true);
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartAbsoluteCurveFittingY2, value);
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
                if (ScenarioChartTrackedMetricY1Axis != model.ScenarioChartTrackedMetricY1Axis)
                {
                    ScenarioChartTrackedMetricY1Axis = model.ScenarioChartTrackedMetricY1Axis;
                }
                if (ScenarioChartTrackedMetricY2Axis != model.ScenarioChartTrackedMetricY2Axis)
                {
                    ScenarioChartTrackedMetricY2Axis = model.ScenarioChartTrackedMetricY2Axis;
                }
                if (ScenarioChartCurveFittingTypeY1 != model.ScenarioChartCurveFittingTypeY1)
                {
                    ScenarioChartCurveFittingTypeY1 = model.ScenarioChartCurveFittingTypeY1;
                }
                if (ScenarioChartCurveFittingTypeY2 != model.ScenarioChartCurveFittingTypeY2)
                {
                    ScenarioChartCurveFittingTypeY2 = model.ScenarioChartCurveFittingTypeY2;
                }
                if (ScenarioChartShowDerivativeY1 != model.ScenarioChartShowDerivativeY1)
                {
                    ScenarioChartShowDerivativeY1 = model.ScenarioChartShowDerivativeY1;
                }
                if (ScenarioChartShowDerivativeY2 != model.ScenarioChartShowDerivativeY2)
                {
                    ScenarioChartShowDerivativeY2 = model.ScenarioChartShowDerivativeY2;
                }
                if (ScenarioChartAbsoluteCurveFittingY1 != model.ScenarioChartAbsoluteCurveFittingY1)
                {
                    ScenarioChartAbsoluteCurveFittingY1 = model.ScenarioChartAbsoluteCurveFittingY1;
                }
                if (ScenarioChartAbsoluteCurveFittingY2 != model.ScenarioChartAbsoluteCurveFittingY2)
                {
                    ScenarioChartAbsoluteCurveFittingY2 = model.ScenarioChartAbsoluteCurveFittingY2;
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
                    ScenarioChartTrackedMetricY1Axis = ScenarioChartTrackedMetricY1Axis,
                    ScenarioChartTrackedMetricY2Axis = ScenarioChartTrackedMetricY2Axis,
                    ScenarioChartCurveFittingTypeY1 = ScenarioChartCurveFittingTypeY1,
                    ScenarioChartCurveFittingTypeY2 = ScenarioChartCurveFittingTypeY2,
                    ScenarioChartShowDerivativeY1 = ScenarioChartShowDerivativeY1,
                    ScenarioChartShowDerivativeY2 = ScenarioChartShowDerivativeY2,
                    ScenarioChartAbsoluteCurveFittingY1 = ScenarioChartAbsoluteCurveFittingY1,
                    ScenarioChartAbsoluteCurveFittingY2 = ScenarioChartAbsoluteCurveFittingY2,
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
                m_SetIsProjectUpdated = null;
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
