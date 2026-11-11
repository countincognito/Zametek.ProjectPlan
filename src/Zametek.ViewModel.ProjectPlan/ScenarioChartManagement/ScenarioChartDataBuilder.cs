using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    // The data of the scenario chart, from the tracked metrics and the settings of the chart: the points of each axis of Y, in
    // the order in which the chart plots them (by X, and then by Y), and the fit of the axis. The chart and the data take
    // their values and their fits from the same functions, and so they always agree.
    internal static class ScenarioChartDataBuilder
    {
        // Null when the chart is empty: it has no metric for the axis of X, or for both axes of Y.
        public static ScenarioChartDataModel? Build(
            TrackedMetricsSetModel trackedMetricsSet,
            TrackedMetrics xMetric,
            TrackedMetrics y1Metric,
            TrackedMetrics y2Metric,
            CurveFittingType curveFittingTypeY1,
            CurveFittingType curveFittingTypeY2,
            bool absoluteCurveFittingY1,
            bool absoluteCurveFittingY2)
        {
            ArgumentNullException.ThrowIfNull(trackedMetricsSet);

            if (xMetric == TrackedMetrics.None
                || (y1Metric == TrackedMetrics.None && y2Metric == TrackedMetrics.None))
            {
                return null;
            }

            Func<MetricsModel, double> xMetricFunction = ScenarioChartManagerViewModel.GetMetricFunction(xMetric);

            return new ScenarioChartDataModel
            {
                XMetric = xMetric,
                Y1 = BuildSeries(trackedMetricsSet, xMetricFunction, y1Metric, curveFittingTypeY1, absoluteCurveFittingY1),
                Y2 = BuildSeries(trackedMetricsSet, xMetricFunction, y2Metric, curveFittingTypeY2, absoluteCurveFittingY2),
            };
        }

        private static ScenarioChartSeriesModel? BuildSeries(
            TrackedMetricsSetModel trackedMetricsSet,
            Func<MetricsModel, double> xMetricFunction,
            TrackedMetrics yMetric,
            CurveFittingType curveFittingType,
            bool absoluteCurveFitting)
        {
            if (yMetric == TrackedMetrics.None)
            {
                return null;
            }

            Func<MetricsModel, double> yMetricFunction = ScenarioChartManagerViewModel.GetMetricFunction(yMetric);

            ScenarioChartPointModel[] points =
            [
                .. trackedMetricsSet.TrackedMetrics
                    .Select(x => new ScenarioChartPointModel
                    {
                        Id = x.NodeId,
                        Path = x.Path,
                        X = xMetricFunction(x.Metrics),
                        Y = yMetricFunction(x.Metrics),
                    })
                    .OrderBy(x => x.X)
                    .ThenBy(x => x.Y),
            ];

            return new ScenarioChartSeriesModel
            {
                Metric = yMetric,
                CurveFittingType = curveFittingType,
                AbsoluteCurveFitting = absoluteCurveFitting,
                Points = points,
                CurveFit = CurveFitCalculator.Fit([.. points.Select(x => x.X)], [.. points.Select(x => x.Y)], curveFittingType),
            };
        }
    }
}
