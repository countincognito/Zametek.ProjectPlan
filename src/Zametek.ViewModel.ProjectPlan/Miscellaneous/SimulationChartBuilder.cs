using ScottPlot;
using ScottPlot.Plottables;
using System.Globalization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Draws the chart of a schedule simulation: a histogram of the finish times, each bar split by the
    /// critical path that set the finish, lines at the mean, at three percentiles, at the planned finish and
    /// at the target, and the cumulative probability (the S-curve) on the axis at the right.
    /// </summary>
    public static class SimulationChartBuilder
    {
        // The percentiles that get a line of their own.
        private static readonly int[] s_PercentileLines = [50, 80, 90];

        private const float c_LineWidth = 2.0f;
        private const double c_BarFill = 0.9;

        public static Plot Build(
            SimulationSummaryModel summary,
            IDateTimeCalculator dateTimeCalculator,
            bool showDates,
            DateTimeOffset projectStart,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(summary);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);

            var plotModel = new Plot();
            plotModel.HideGrid();

            if (!summary.HasResult || summary.Bins.Count == 0)
            {
                return plotModel.SetBaseTheme(baseTheme);
            }

            double X(int day) => ChartHelper.CalculateChartFinishTimeXValue(day, showDates, projectStart, dateTimeCalculator);

            // The axes.

            IXAxis xAxis = showDates ? plotModel.Axes.DateTimeTicksBottom() : plotModel.Axes.Bottom;
            xAxis.Label.Text = Resource.ProjectPlan.Labels.Label_TimeAxisTitle;
            xAxis.Label.FontSize = PlotHelper.FontSize;
            xAxis.Label.Bold = false;

            IYAxis countAxis = plotModel.Axes.Left;
            countAxis.Label.Text = Resource.ProjectPlan.Labels.Label_SimulationIterationsAxisTitle;
            countAxis.Label.FontSize = PlotHelper.FontSize;
            countAxis.Label.Bold = false;

            IYAxis probabilityAxis = plotModel.Axes.Right;
            probabilityAxis.Label.Text = Resource.ProjectPlan.Labels.Label_SimulationCumulativeAxisTitle;
            probabilityAxis.Label.FontSize = PlotHelper.FontSize;
            probabilityAxis.Label.Bold = false;

            plotModel.Legend.OutlineWidth = 1;
            plotModel.Legend.BackgroundColor = Colors.Transparent;
            plotModel.Legend.ShadowColor = Colors.Transparent;
            plotModel.Legend.ShadowOffset = new(0, 0);
            plotModel.ShowLegend(Edge.Right);

            // The theme is applied here, before the plottables are added, and not at the end as the other charts do:
            // it paints every vertical line in the colour of the text, and these lines are told apart by colour.
            plotModel.SetBaseTheme(baseTheme);
            Color foreground = baseTheme == BaseTheme.Dark
                ? ColorHelper.ScottPlotDarkThemeForegroundColor
                : ColorHelper.ScottPlotLightThemeForegroundColor;

            // The histogram: one bar for each group of paths in each bin, stacked on the groups before it.

            int groupCount = summary.ChartPathCount + 1;
            var stackBase = new double[summary.Bins.Count];
            var colors = new PresetColorSequence();

            for (int group = 0; group < groupCount; group++)
            {
                bool isOtherGroup = group == summary.ChartPathCount;
                Color color = isOtherGroup
                    ? Colors.Gray
                    : ColorHelper.ColorFormatToScottPlotColor(colors.Next());

                var bars = new List<Bar>();
                double groupWeight = 0.0;

                for (int b = 0; b < summary.Bins.Count; b++)
                {
                    SimulationHistogramBinModel bin = summary.Bins[b];
                    double weight = group < bin.PathGroupWeights.Count ? bin.PathGroupWeights[group] : 0.0;

                    if (weight <= 0.0)
                    {
                        continue;
                    }

                    int binDays = bin.LastFinishTime - bin.FirstFinishTime + 1;

                    bars.Add(new Bar
                    {
                        Position = X(bin.FirstFinishTime) + ((binDays - 1) / 2.0),
                        ValueBase = stackBase[b],
                        Value = stackBase[b] + weight,
                        Size = binDays * c_BarFill,
                        FillColor = color,
                        LineWidth = 0,
                    });

                    stackBase[b] += weight;
                    groupWeight += weight;
                }

                if (bars.Count == 0)
                {
                    continue;
                }

                BarPlot barPlot = plotModel.Add.Bars(bars);
                barPlot.LegendText = LegendTextOf(summary, group, isOtherGroup, groupWeight);
            }

            // The S-curve, on the axis at the right.

            double[] curveX = [.. summary.Bins.Select(x => X(x.LastFinishTime))];
            double[] curveY = [.. summary.Bins.Select(x => x.CumulativeShare * 100.0)];
            Scatter curve = plotModel.Add.Scatter(curveX, curveY);
            curve.Axes.YAxis = probabilityAxis;
            curve.LineWidth = c_LineWidth;
            curve.Color = foreground.WithAlpha(170);
            curve.MarkerSize = 0;

            // The lines.

            AddLine(
                plotModel,
                X((int)Math.Floor(summary.Mean)) + (summary.Mean - Math.Floor(summary.Mean)),
                $@"{Resource.ProjectPlan.Labels.Label_SimulationMean} {summary.Mean.ToString(@"0.0", CultureInfo.CurrentCulture)}",
                foreground,
                LinePattern.Dashed);

            Color[] percentileColors = [Colors.Orange, Colors.OrangeRed, Colors.DarkRed];
            for (int i = 0; i < s_PercentileLines.Length; i++)
            {
                SimulationPercentileModel? percentile = summary.Percentiles.FirstOrDefault(x => x.Percentile == s_PercentileLines[i]);

                if (percentile is not null)
                {
                    AddLine(
                        plotModel,
                        X(percentile.FinishTime),
                        $@"{string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Labels.Label_SimulationPercentile, percentile.Percentile)} {percentile.FinishTime.ToString(CultureInfo.CurrentCulture)}",
                        percentileColors[i],
                        LinePattern.Dotted);
                }
            }

            AddLine(
                plotModel,
                X(summary.PlannedFinishTime),
                $@"{Resource.ProjectPlan.Labels.Label_SimulationPlannedLine} {summary.PlannedFinishTime.ToString(CultureInfo.CurrentCulture)}",
                Colors.Green,
                LinePattern.Solid);

            if (summary.TargetFinishTime is int target)
            {
                AddLine(
                    plotModel,
                    X(target),
                    $@"{Resource.ProjectPlan.Labels.Label_SimulationTargetLine} {target.ToString(CultureInfo.CurrentCulture)}",
                    Colors.Blue,
                    LinePattern.Solid);
            }

            // The limits: a margin of a bin on each side of the histogram, from zero up, and the whole of the
            // probability axis.

            int first = Math.Min(summary.Minimum, summary.PlannedFinishTime);
            int last = Math.Max(summary.Maximum, summary.PlannedFinishTime);
            if (summary.TargetFinishTime is int targetTime)
            {
                first = Math.Min(first, targetTime);
                last = Math.Max(last, targetTime);
            }

            double margin = Math.Max(1.0, summary.BinWidth);
            plotModel.Axes.SetLimitsX(X(first) - margin, X(last) + margin, xAxis);
            plotModel.Axes.SetLimitsY(0.0, Math.Max(1.0, stackBase.Max()) * 1.08, countAxis);
            plotModel.Axes.SetLimitsY(0.0, 100.0, probabilityAxis);

            return plotModel;
        }

        private static string LegendTextOf(
            SimulationSummaryModel summary,
            int group,
            bool isOtherGroup,
            double groupWeight)
        {
            double share = groupWeight / summary.ValidIterationCount;
            string text = isOtherGroup
                ? Resource.ProjectPlan.Labels.Label_SimulationOtherPaths
                : summary.Paths[group].ShortLabel;

            string prefix = isOtherGroup ? string.Empty : $@"#{summary.Paths[group].Rank.ToString(CultureInfo.CurrentCulture)} ";

            return $@"{prefix}{text} ({share.ToString(@"P0", CultureInfo.CurrentCulture)})";
        }

        private static void AddLine(
            Plot plotModel,
            double x,
            string legendText,
            Color color,
            LinePattern pattern)
        {
            VerticalLine line = plotModel.Add.VerticalLine(x, width: c_LineWidth, color: color, pattern: pattern);
            line.LegendText = legendText;
        }
    }
}
