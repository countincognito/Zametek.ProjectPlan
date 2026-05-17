namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectDisplaySettingsModel
    {
        public SortMode ProjectScenarioSortMode { get; init; } = default;

        public SortDirection ProjectScenarioSortDirection { get; init; } = default;


        public bool ScenarioChartShowNames { get; init; } = default;

        public TrackedMetrics ScenarioChartTrackedMetricXAxis { get; init; } = default;

        public TrackedMetrics ScenarioChartTrackedMetricYAxis { get; init; } = default;

        public CurveFittingType ScenarioChartCurveFittingType { get; init; } = default;
    }
}
