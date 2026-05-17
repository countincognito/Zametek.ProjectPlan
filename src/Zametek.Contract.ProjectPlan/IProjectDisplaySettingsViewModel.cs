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
        public TrackedMetrics ScenarioChartTrackedMetricYAxis { get; set; }
        public CurveFittingType ScenarioChartCurveFittingType { get; set; }


        void SetValues(ProjectDisplaySettingsModel model);
        ProjectDisplaySettingsModel GetValues();
    }
}
