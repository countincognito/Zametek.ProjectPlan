using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Writes the data of the scenario chart as CSV, as <see cref="SimulationCsvWriter"/> writes the files of a simulation: the
    /// points of each axis of Y, and the fit of each axis of Y. The metrics have the names of <see cref="TrackedMetricsNames"/>.
    /// A list of coefficients has a space between two of them, from the constant up. A value that is not a number is empty. A
    /// chart with no data has files with only their headers.
    /// </summary>
    public static class ScenarioChartCsvWriter
    {
        /// <summary>
        /// One row for each point of each axis of Y, in the order of the chart: the axis (Y1 or Y2), the id and the path of the
        /// tracked scenario, and its values of X and Y.
        /// </summary>
        public static void WritePoints(
            TextWriter writer,
            ScenarioChartDataModel? data)
        {
            ArgumentNullException.ThrowIfNull(writer);

            CsvText.WriteRow(writer, @"Axis", @"Id", @"Path", @"X", @"Y");

            foreach ((string axis, ScenarioChartSeriesModel series) in Axes(data))
            {
                foreach (ScenarioChartPointModel point in series.Points)
                {
                    CsvText.WriteRow(writer, axis, point.Id.ToString(), point.Path, Number(point.X), Number(point.Y));
                }
            }
        }

        /// <summary>
        /// One row for each axis of Y: the axis, the metrics of X and of Y, the fit that the chart asks for, whether the chart
        /// shows its absolute value, and r², the coefficients of the curve and those of its derivative. An axis with no fit has
        /// no values for the last three.
        /// </summary>
        public static void WriteFits(
            TextWriter writer,
            ScenarioChartDataModel? data)
        {
            ArgumentNullException.ThrowIfNull(writer);

            CsvText.WriteRow(
                writer,
                @"Axis",
                @"XMetric",
                @"YMetric",
                @"CurveFitting",
                @"Absolute",
                @"RSquared",
                @"Coefficients",
                @"DerivativeCoefficients");

            foreach ((string axis, ScenarioChartSeriesModel series) in Axes(data))
            {
                CurveFitModel? fit = series.CurveFit;

                CsvText.WriteRow(
                    writer,
                    axis,
                    TrackedMetricsNames.Get(data!.XMetric),
                    TrackedMetricsNames.Get(series.Metric),
                    series.CurveFittingType.ToString(),
                    CsvText.Boolean(series.AbsoluteCurveFitting),
                    fit is null ? string.Empty : Number(fit.RSquared),
                    fit is null ? string.Empty : Numbers(fit.Coefficients),
                    fit is null ? string.Empty : Numbers(fit.DerivativeCoefficients));
            }
        }

        private static IEnumerable<(string Axis, ScenarioChartSeriesModel Series)> Axes(ScenarioChartDataModel? data)
        {
            if (data?.Y1 is ScenarioChartSeriesModel y1)
            {
                yield return (@"Y1", y1);
            }

            if (data?.Y2 is ScenarioChartSeriesModel y2)
            {
                yield return (@"Y2", y2);
            }
        }

        private static string Number(double value) => double.IsFinite(value) ? CsvText.Number(value) : string.Empty;

        private static string Numbers(IEnumerable<double> values) => string.Join(' ', values.Select(Number));
    }
}
