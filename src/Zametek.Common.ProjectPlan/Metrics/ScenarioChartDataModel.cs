namespace Zametek.Common.ProjectPlan
{
    // The data of the scenario chart, from the settings of the chart that the plan keeps: the metric of the axis of X, and
    // each axis of Y that has a metric. A client can read the trade-off of time, cost and risk from it, and does not have to
    // fit curves of its own.
    [Serializable]
    public record ScenarioChartDataModel
    {
        public TrackedMetrics XMetric { get; init; }

        public ScenarioChartSeriesModel? Y1 { get; init; }

        public ScenarioChartSeriesModel? Y2 { get; init; }
    }
}
