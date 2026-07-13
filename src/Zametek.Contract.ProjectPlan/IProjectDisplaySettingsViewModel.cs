using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectDisplaySettingsViewModel
        : IDisposable
    {
        public SortMode ProjectScenarioSortMode { get; set; }
        public SortDirection ProjectScenarioSortDirection { get; set; }


        public bool ScenarioChartShowNames { get; set; }
        public TrackedMetrics ScenarioChartTrackedMetricXAxis { get; set; }
        public TrackedMetrics ScenarioChartTrackedMetricY1Axis { get; set; }
        public TrackedMetrics ScenarioChartTrackedMetricY2Axis { get; set; }
        public CurveFittingType ScenarioChartCurveFittingTypeY1 { get; set; }
        public CurveFittingType ScenarioChartCurveFittingTypeY2 { get; set; }


        void SetValues(ProjectDisplaySettingsModel model);
        ProjectDisplaySettingsModel GetValues();
    }
}
