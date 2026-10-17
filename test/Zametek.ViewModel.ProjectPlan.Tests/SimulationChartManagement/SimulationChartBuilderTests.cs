using ScottPlot;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The chart of a simulation: what is on it, and what its legend says.
    /// </summary>
    public class SimulationChartBuilderTests
    {
        private static readonly IReadOnlyDictionary<int, string> s_Names = new Dictionary<int, string>
        {
            [10] = "A",
            [20] = "B",
            [30] = "C",
        };

        private static CriticalPath<int> Path(params (int Id, CriticalPathLink Link)[] steps) =>
            new(steps.Select(x => new CriticalPathStep<int>(x.Id, x.Link)));

        // Ten iterations with three paths (A > B, A > C, and A ~> C), one that failed, one with no path, and two ties.
        private static SimulationSummaryModel Summary(int? target = null)
        {
            int[] finishTimes = [10, 11, 11, 12, 12, 12, 13, -1, 10, 11];
            int[][] pathsOfIteration = [[0], [0], [0, 1], [1], [1], [1, 2], [2], [], [0], []];

            var offsets = new List<int> { 0 };
            var indexes = new List<int>();
            foreach (int[] paths in pathsOfIteration)
            {
                indexes.AddRange(paths);
                offsets.Add(indexes.Count);
            }

            var result = new SimulationResult<int>(
                [10, 20, 30],
                finishTimes,
                new ulong[10],
                [
                    Path((10, CriticalPathLink.None), (20, CriticalPathLink.Dependency)),
                    Path((10, CriticalPathLink.None), (30, CriticalPathLink.Dependency)),
                    Path((10, CriticalPathLink.None), (30, CriticalPathLink.Resource)),
                ],
                [.. offsets],
                [.. indexes],
                [true, true, true, true, true, true, true, true, true, true]);

            return SimulationAnalyzer.Analyze(result, s_Names, plannedFinishTime: 11, targetFinishTime: target);
        }

        private static Plot Build(SimulationSummaryModel summary, bool showDates = false, BaseTheme theme = BaseTheme.Light) =>
            SimulationChartBuilder.Build(
                summary,
                new DateTimeCalculator(TimeProvider.System),
                showDates,
                new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero),
                theme);

        private static List<string> LegendTexts(Plot plot) =>
            [.. plot.GetPlottables()
                .SelectMany(x => x.LegendItems)
                .Select(x => x.LabelText)];

        [Fact]
        public void Build_Given_ASummary_Then_HasABarPlotForEachGroupOfPathsTheSCurveAndTheLines()
        {
            using Plot plot = Build(Summary(target: 12));

            // Four groups (three paths and the rest), the S-curve, and the lines: mean, P50, P80, P90, plan, target.
            plot.GetPlottables().OfType<ScottPlot.Plottables.BarPlot>().Count().ShouldBe(4);
            plot.GetPlottables().OfType<ScottPlot.Plottables.Scatter>().Count().ShouldBe(1);
            plot.GetPlottables().OfType<ScottPlot.Plottables.VerticalLine>().Count().ShouldBe(6);
        }

        [Fact]
        public void Build_Given_NoTarget_Then_HasNoTargetLine()
        {
            using Plot plot = Build(Summary(target: null));

            plot.GetPlottables().OfType<ScottPlot.Plottables.VerticalLine>().Count().ShouldBe(5);
            LegendTexts(plot).ShouldNotContain(x => x.StartsWith(Resource.ProjectPlan.Labels.Label_SimulationTargetLine));
        }

        [Fact]
        public void Build_Given_ShortPathNames_Then_TheLegendShowsThemWithTheRankAndTheShare()
        {
            using Plot plot = Build(Summary());

            List<string> legend = LegendTexts(plot);

            // The paths weigh 3.5, 3 and 1.5 of the 9 valid iterations, and the iteration with no path goes to the rest.
            string Share(double weight) => (weight / 9.0).ToString("P0", CultureInfo.CurrentCulture);

            legend.ShouldContain($"#1 A > B ({Share(3.5)})");
            legend.ShouldContain($"#2 A > C ({Share(3.0)})");
            legend.ShouldContain($"#3 A ~> C ({Share(1.5)})");
            legend.ShouldContain($"{Resource.ProjectPlan.Labels.Label_SimulationOtherPaths} ({Share(1.0)})");
        }

        [Fact]
        public void Build_Given_LongPathNames_Then_TheLegendShowsOnlyTheRankAndTheShare()
        {
            int[] ids = [.. Enumerable.Range(1, 10)];
            var names = ids.ToDictionary(x => x, x => $"Activity number {x}");
            var path = new CriticalPath<int>(ids.Select((x, i) => new CriticalPathStep<int>(x, i == 0 ? CriticalPathLink.None : CriticalPathLink.Dependency)));
            var result = new SimulationResult<int>(ids, [30, 31], new ulong[2], [path], [0, 1, 2], [0, 0], [true, true]);
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(result, names, 30, null);

            using Plot plot = Build(summary);

            LegendTexts(plot).ShouldContain($"#1 ({1.0.ToString("P0", CultureInfo.CurrentCulture)})");
        }

        [Fact]
        public void Build_Given_TheLines_Then_TheLegendNamesThem()
        {
            using Plot plot = Build(Summary(target: 12));

            List<string> legend = LegendTexts(plot);

            legend.ShouldContain(x => x.StartsWith(Resource.ProjectPlan.Labels.Label_SimulationMean));
            legend.ShouldContain($"{string.Format(Resource.ProjectPlan.Labels.Label_SimulationPercentile, 50)} 11");
            legend.ShouldContain($"{string.Format(Resource.ProjectPlan.Labels.Label_SimulationPercentile, 80)} 12");
            legend.ShouldContain($"{string.Format(Resource.ProjectPlan.Labels.Label_SimulationPercentile, 90)} 13");
            legend.ShouldContain($"{Resource.ProjectPlan.Labels.Label_SimulationPlannedLine} 11");
            legend.ShouldContain($"{Resource.ProjectPlan.Labels.Label_SimulationTargetLine} 12");
        }

        [Fact]
        public void Build_Given_AStack_Then_EachBarOfAGroupStartsWhereTheGroupBelowItEnds()
        {
            using Plot plot = Build(Summary());

            List<ScottPlot.Plottables.BarPlot> barPlots = [.. plot.GetPlottables().OfType<ScottPlot.Plottables.BarPlot>()];
            // Per bin the bars of the groups, in the order of the groups, stack to the number of iterations of the bin.
            Dictionary<double, List<(double Base, double Top)>> byPosition = [];
            foreach (ScottPlot.Plottables.BarPlot barPlot in barPlots)
            {
                foreach (Bar bar in barPlot.Bars)
                {
                    if (!byPosition.TryGetValue(bar.Position, out List<(double Base, double Top)>? stack))
                    {
                        stack = [];
                        byPosition[bar.Position] = stack;
                    }
                    stack.Add((bar.ValueBase, bar.Value));
                }
            }

            foreach (KeyValuePair<double, List<(double Base, double Top)>> pair in byPosition)
            {
                double top = 0.0;
                foreach ((double bottom, double value) in pair.Value)
                {
                    bottom.ShouldBe(top, 1e-12);
                    top = value;
                }
            }

            // 10, 11, 12 and 13: 2, 3, 3 and 1 iterations.
            byPosition.Values.Select(x => x[^1].Top).Order().ShouldBe([1.0, 2.0, 3.0, 3.0], 1e-12);
        }

        [Fact]
        public void Build_Given_DatesAndEitherTheme_Then_BuildsTheSamePlottables()
        {
            SimulationSummaryModel summary = Summary(target: 12);

            using Plot days = Build(summary, showDates: false);
            using Plot dates = Build(summary, showDates: true);
            using Plot dark = Build(summary, showDates: false, theme: BaseTheme.Dark);

            dates.GetPlottables().Count().ShouldBe(days.GetPlottables().Count());
            dark.GetPlottables().Count().ShouldBe(days.GetPlottables().Count());
        }

        [Fact]
        public void Build_Given_NoResult_Then_IsAnEmptyChart()
        {
            var empty = new SimulationSummaryModel { IterationCount = 3, FailedIterationCount = 3 };

            using Plot plot = Build(empty);

            plot.GetPlottables().ShouldBeEmpty();
        }

        [Fact]
        public void Build_Given_TheChart_Then_CanBeDrawnToAnImage()
        {
            using Plot plot = Build(Summary(target: 12));

            byte[] image = plot.GetImageBytes(800, 500, ImageFormat.Png);

            image.Length.ShouldBeGreaterThan(1000);
        }
    }
}
