using System.Text.Json.Serialization;
using Zametek.Common.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // The data of the scenario chart as zpp serve's API gives it, from the settings of the chart that the project keeps: the
    // metric of the axis of X, and each axis of Y that has a metric, with its points and its fit. A metric has the name that it
    // has in the metrics, and so a client finds its value there. The values are numbers and not the text that zpp prints. A
    // value that is not a number is null, because JSON cannot write it.
    public record ScenarioChartResponse(
        string XMetric,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ScenarioChartSeriesResponse? Y1,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ScenarioChartSeriesResponse? Y2)
    {
        public static ScenarioChartResponse? From(ScenarioChartDataModel? data)
        {
            return data is null
                ? null
                : new ScenarioChartResponse(
                    MetricName(data.XMetric),
                    ScenarioChartSeriesResponse.From(data.Y1),
                    ScenarioChartSeriesResponse.From(data.Y2));
        }

        // The name of a metric of the chart, as the metrics of the API name it.
        internal static string MetricName(TrackedMetrics metric)
        {
            return ApiNamingPolicy.Instance.ConvertName(TrackedMetricsNames.Get(metric));
        }

        internal static double? Finite(double value)
        {
            return double.IsFinite(value) ? value : null;
        }
    }

    // An axis of Y: its metric, the fit that the chart asks for, whether the chart shows the absolute value of the curve, the
    // points in the order of the chart (by X, and then by Y), and the fit. An axis has no fit when it asks for none, when the
    // points have too few different values of X, or when the fit is not a curve.
    public record ScenarioChartSeriesResponse(
        string Metric,
        CurveFittingType CurveFitting,
        bool Absolute,
        IReadOnlyList<ScenarioChartPointResponse> Points,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CurveFitResponse? Fit)
    {
        internal static ScenarioChartSeriesResponse? From(ScenarioChartSeriesModel? series)
        {
            return series is null
                ? null
                : new ScenarioChartSeriesResponse(
                    ScenarioChartResponse.MetricName(series.Metric),
                    series.CurveFittingType,
                    series.AbsoluteCurveFitting,
                    [.. series.Points.Select(x => new ScenarioChartPointResponse(x.Id, x.Path, ScenarioChartResponse.Finite(x.X), ScenarioChartResponse.Finite(x.Y)))],
                    series.CurveFit is CurveFitModel fit
                        ? new CurveFitResponse(ScenarioChartResponse.Finite(fit.RSquared), fit.Coefficients, fit.DerivativeCoefficients)
                        : null);
        }
    }

    // A point of the chart: a tracked scenario, by its id and its path, and the values of the metrics of the axes.
    public record ScenarioChartPointResponse(
        Guid Id,
        string Path,
        double? X,
        double? Y);

    // The fit of an axis: r², the coefficients of the curve and those of its derivative, from the constant up. A fit has
    // only finite coefficients. API.md gives the form of the curve of each fit.
    public record CurveFitResponse(
        double? RSquared,
        IReadOnlyList<double> Coefficients,
        IReadOnlyList<double> DerivativeCoefficients);
}
