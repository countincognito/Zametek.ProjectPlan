namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record AppSettingsModel
    {
        public string Version { get; init; } = string.Empty;

        public string ProjectDirectory { get; init; } = string.Empty;

        public bool DefaultShowDates { get; init; } = false;

        public bool DefaultUseClassicDates { get; init; } = false;

        public NonWorkingDayMode DefaultNonWorkingDayMode { get; init; } = default;

        public bool DefaultHideCost { get; init; } = false;

        public bool DefaultHideBilling { get; init; } = false;

        public SortMode ProjectScenarioSortMode { get; init; } = default;

        public SortDirection ProjectScenarioSortDirection { get; init; } = default;

        public TrackedMetrics ScenarioChartTrackedMetricXAxis { get; init; } = default;

        public TrackedMetrics ScenarioChartTrackedMetricYAxis { get; init; } = default;

        public CurveFittingType ScenarioChartCurveFittingType { get; init; } = default;

        public string SelectedTheme { get; init; } = string.Empty;
    }
}
