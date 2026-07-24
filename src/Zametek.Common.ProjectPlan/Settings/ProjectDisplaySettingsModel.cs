namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectDisplaySettingsModel
    {
        public SortMode ProjectScenarioSortMode { get; init; } = default;

        public SortDirection ProjectScenarioSortDirection { get; init; } = default;


        public bool ScenarioChartShowNames { get; init; } = default;

        public TrackedMetrics ScenarioChartTrackedMetricXAxis { get; init; } = default;

        public TrackedMetrics ScenarioChartTrackedMetricY1Axis { get; init; } = default;

        public TrackedMetrics ScenarioChartTrackedMetricY2Axis { get; init; } = default;

        public CurveFittingType ScenarioChartCurveFittingTypeY1 { get; init; } = default;

        public CurveFittingType ScenarioChartCurveFittingTypeY2 { get; init; } = default;

        public bool ScenarioChartShowDerivativeY1 { get; init; } = default;

        public bool ScenarioChartShowDerivativeY2 { get; init; } = default;

        public bool ScenarioChartAbsoluteCurveFittingY1 { get; init; } = default;

        public bool ScenarioChartAbsoluteCurveFittingY2 { get; init; } = default;
    }
}
