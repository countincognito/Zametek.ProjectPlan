using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The statistics of a simulation, on results that are small enough to work out by hand.
    /// </summary>
    public class SimulationAnalyzerTests
    {
        private static readonly IReadOnlyDictionary<int, string> s_Names = new Dictionary<int, string>
        {
            [10] = "A",
            [20] = "B",
            [30] = "C",
        };

        private static CriticalPath<int> Path(params (int Id, CriticalPathLink Link)[] steps) =>
            new(steps.Select(x => new CriticalPathStep<int>(x.Id, x.Link)));

        // Ten iterations of a plan of three activities (10, 20 and 30), with three paths:
        //   0: A > B,  1: A > C,  2: A ~> C (a resource link).
        // Iteration 2 and 5 have two paths each (a tie), iteration 7 failed, and iteration 9 has no path.
        private static SimulationResult<int> TenIterations()
        {
            int[] finishTimes = [10, 11, 11, 12, 12, 12, 13, -1, 10, 11];
            int[][] pathsOfIteration = [[0], [0], [0, 1], [1], [1], [1, 2], [2], [], [0], []];
            ulong[] bits = [0b011, 0b011, 0b111, 0b101, 0b101, 0b101, 0b101, 0, 0b011, 0b001];

            var offsets = new List<int> { 0 };
            var indexes = new List<int>();
            foreach (int[] paths in pathsOfIteration)
            {
                indexes.AddRange(paths);
                offsets.Add(indexes.Count);
            }

            return new SimulationResult<int>(
                [10, 20, 30],
                finishTimes,
                bits,
                [
                    Path((10, CriticalPathLink.None), (20, CriticalPathLink.Dependency)),
                    Path((10, CriticalPathLink.None), (30, CriticalPathLink.Dependency)),
                    Path((10, CriticalPathLink.None), (30, CriticalPathLink.Resource)),
                ],
                [.. offsets],
                [.. indexes],
                [true, true, true, true, true, true, true, true, true, true]);
        }

        [Fact]
        public void Analyze_Given_AResult_Then_HasTheStatisticsOfTheValidIterations()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(TenIterations(), s_Names, plannedFinishTime: 11, targetFinishTime: null);

            summary.IterationCount.ShouldBe(10);
            summary.ValidIterationCount.ShouldBe(9);
            summary.FailedIterationCount.ShouldBe(1);
            summary.PlannedFinishTime.ShouldBe(11);
            summary.HasResult.ShouldBeTrue();

            // 10, 10, 11, 11, 11, 12, 12, 12, 13.
            summary.Mean.ShouldBe(102.0 / 9.0, 1e-12);
            summary.StandardDeviation.ShouldBe(Math.Sqrt(8.0 / 9.0), 1e-12);
            summary.Minimum.ShouldBe(10);
            summary.Maximum.ShouldBe(13);
        }

        [Fact]
        public void Analyze_Given_AResult_Then_ThePercentilesAreTheNearestRank()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(TenIterations(), s_Names, 11, null);

            Dictionary<int, int> percentiles = summary.Percentiles.ToDictionary(x => x.Percentile, x => x.FinishTime);

            percentiles[5].ShouldBe(10);
            percentiles[10].ShouldBe(10);
            percentiles[25].ShouldBe(11);
            percentiles[50].ShouldBe(11);
            percentiles[75].ShouldBe(12);
            percentiles[80].ShouldBe(12);
            percentiles[90].ShouldBe(13);
            percentiles[95].ShouldBe(13);
            percentiles[99].ShouldBe(13);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(7)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(95)]
        [InlineData(100)]
        public void Percentile_Given_OneHundredValues_Then_IsTheValueAtThatRank(int percentile)
        {
            int[] values = [.. Enumerable.Range(1, 100)];

            SimulationAnalyzer.Percentile(values, percentile).ShouldBe(percentile);
        }

        [Fact]
        public void Percentile_Given_NoValues_Then_Throws()
        {
            Should.Throw<ArgumentException>(() => SimulationAnalyzer.Percentile([], 50));
        }

        [Fact]
        public void Analyze_Given_ATarget_Then_GivesTheShareOfIterationsThatFinishByIt()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(TenIterations(), s_Names, 11, targetFinishTime: 11);

            summary.TargetFinishTime.ShouldBe(11);
            summary.TargetProbability.ShouldNotBeNull();
            summary.TargetProbability.Value.ShouldBe(5.0 / 9.0, 1e-12);
        }

        [Fact]
        public void Analyze_Given_NoTarget_Then_HasNoProbability()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(TenIterations(), s_Names, 11, null);

            summary.TargetFinishTime.ShouldBeNull();
            summary.TargetProbability.ShouldBeNull();
        }

        [Fact]
        public void Analyze_Given_AResult_Then_TheHistogramHasABinForEachDayAndTheSCurveEndsAtOne()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(TenIterations(), s_Names, 11, null);

            summary.BinWidth.ShouldBe(1);
            summary.Bins.Select(x => x.FirstFinishTime).ShouldBe([10, 11, 12, 13]);
            summary.Bins.Select(x => x.LastFinishTime).ShouldBe([10, 11, 12, 13]);
            summary.Bins.Select(x => x.Count).ShouldBe([2, 3, 3, 1]);
            summary.Bins.Select(x => x.CumulativeShare).ShouldBe([2.0 / 9.0, 5.0 / 9.0, 8.0 / 9.0, 1.0], 1e-12);
        }

        [Fact]
        public void Analyze_Given_TiedPaths_Then_TheWeightOfAnIterationIsSplitEquallyAmongThem()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(TenIterations(), s_Names, 11, null);

            summary.ChartPathCount.ShouldBe(3);

            // The groups are, in the order of the weight of the paths, A > B, A > C and A ~> C, and then the rest.
            summary.Bins[0].PathGroupWeights.ShouldBe([2.0, 0.0, 0.0, 0.0], 1e-12);
            summary.Bins[1].PathGroupWeights.ShouldBe([1.5, 0.5, 0.0, 1.0], 1e-12);
            summary.Bins[2].PathGroupWeights.ShouldBe([0.0, 2.5, 0.5, 0.0], 1e-12);
            summary.Bins[3].PathGroupWeights.ShouldBe([0.0, 0.0, 1.0, 0.0], 1e-12);

            // The weights of the groups in a bin add up to the iterations in it.
            foreach (SimulationHistogramBinModel bin in summary.Bins)
            {
                bin.PathGroupWeights.Sum().ShouldBe(bin.Count, 1e-12);
            }
        }

        [Fact]
        public void Analyze_Given_AResult_Then_ThePathTableGivesTheSharesThatAddUpToOne()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(TenIterations(), s_Names, 11, null);

            summary.Paths.Select(x => x.Label).ShouldBe(["A > B", "A > C", "A ~> C"]);
            summary.Paths.Select(x => x.Rank).ShouldBe([1, 2, 3]);
            summary.Paths.Select(x => x.HasResourceLinks).ShouldBe([false, false, true]);
            summary.Paths.Select(x => x.Weight).ShouldBe([3.5, 3.0, 1.5], 1e-12);
            summary.Paths.Select(x => x.Share).ShouldBe([3.5 / 9.0, 3.0 / 9.0, 1.5 / 9.0], 1e-12);

            // The iteration with no path is the only weight that no listed path has.
            summary.OtherPathsWeight.ShouldBe(1.0, 1e-12);
            (summary.Paths.Sum(x => x.Share) + (summary.OtherPathsWeight / summary.ValidIterationCount)).ShouldBe(1.0, 1e-12);
        }

        [Fact]
        public void Analyze_Given_AResult_Then_TheCriticalityIndexCountsTheValidIterationsOfEachActivity()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(TenIterations(), s_Names, 11, null);

            summary.Activities.Select(x => x.Name).ShouldBe(["A", "C", "B"]);
            summary.Activities.Select(x => x.CriticalIterationCount).ShouldBe([9, 5, 4]);
            summary.Activities.Select(x => x.CriticalityIndex).ShouldBe([1.0, 5.0 / 9.0, 4.0 / 9.0], 1e-12);
            summary.Activities.Select(x => x.ActivityId).ShouldBe([10, 30, 20]);
        }

        [Fact]
        public void Analyze_Given_AnActivityWithNoName_Then_UsesItsNumber()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(TenIterations(), new Dictionary<int, string> { [10] = "A", [20] = " " }, 11, null);

            summary.Activities.Select(x => x.Name).ShouldBe(["A", "#30", "#20"]);
            summary.Paths[0].Label.ShouldBe("A > #20");
        }

        [Fact]
        public void Analyze_Given_APathOfManyActivities_Then_TheShortLabelKeepsTheEnds()
        {
            int[] ids = [.. Enumerable.Range(1, 8)];
            var names = ids.ToDictionary(x => x, x => ((char)('A' + x - 1)).ToString());
            var path = new CriticalPath<int>(ids.Select((x, i) => new CriticalPathStep<int>(x, i == 0 ? CriticalPathLink.None : CriticalPathLink.Dependency)));
            var result = new SimulationResult<int>(ids, [20], [0b1111_1111UL], [path], [0, 1], [0], [true]);

            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(result, names, 20, null);

            summary.Paths[0].Label.ShouldBe("A > B > C > D > E > F > G > H");
            summary.Paths[0].ShortLabel.ShouldBe("A > B > … > G > H");
        }

        [Fact]
        public void Analyze_Given_MoreThanEightPaths_Then_TheChartShowsEightAndTheRestShareOneGroup()
        {
            // Ten iterations, each with a path of its own, and the weights 10, 9, ... 1 by repeating iterations.
            var finishTimes = new List<int>();
            var offsets = new List<int> { 0 };
            var indexes = new List<int>();
            var paths = new List<CriticalPath<int>>();
            for (int p = 0; p < 10; p++)
            {
                paths.Add(Path((10, CriticalPathLink.None), (20 + p, CriticalPathLink.Dependency)));
                for (int repeat = 0; repeat < 10 - p; repeat++)
                {
                    finishTimes.Add(5);
                    indexes.Add(p);
                    offsets.Add(indexes.Count);
                }
            }
            var result = new SimulationResult<int>([10], [.. finishTimes], null, paths, [.. offsets], [.. indexes], [.. finishTimes.Select(_ => true)]);

            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(result, s_Names, 5, null);

            summary.ChartPathCount.ShouldBe(8);
            summary.Bins.Count.ShouldBe(1);
            summary.Bins[0].PathGroupWeights.Count.ShouldBe(9);
            summary.Bins[0].PathGroupWeights.Take(8).ShouldBe([10.0, 9.0, 8.0, 7.0, 6.0, 5.0, 4.0, 3.0], 1e-12);
            summary.Bins[0].PathGroupWeights[8].ShouldBe(3.0, 1e-12);
            summary.Paths.Count.ShouldBe(10);
            summary.Activities.ShouldBeEmpty();
        }

        [Fact]
        public void Analyze_Given_APathsOfEqualWeight_Then_TheOneThatAppearedFirstComesFirst()
        {
            var result = new SimulationResult<int>(
                [10],
                [5, 5],
                null,
                [Path((10, CriticalPathLink.None), (20, CriticalPathLink.Dependency)), Path((10, CriticalPathLink.None), (30, CriticalPathLink.Dependency))],
                [0, 1, 2],
                [1, 0],
                [true, true]);

            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(result, s_Names, 5, null);

            summary.Paths.Select(x => x.Label).ShouldBe(["A > B", "A > C"]);
        }

        [Fact]
        public void Analyze_Given_AWideRangeOfFinishTimes_Then_TheBinsAreWiderThanADay()
        {
            int[] finishTimes = [.. Enumerable.Range(0, 251)];
            var result = new SimulationResult<int>([10], finishTimes, null, [], null, null, null);

            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(result, s_Names, 100, null);

            summary.BinWidth.ShouldBe(3);
            summary.Bins.Count.ShouldBe(84);
            summary.Bins[0].FirstFinishTime.ShouldBe(0);
            summary.Bins[0].LastFinishTime.ShouldBe(2);
            summary.Bins[^1].FirstFinishTime.ShouldBe(249);
            summary.Bins[^1].LastFinishTime.ShouldBe(250);
            summary.Bins.Sum(x => x.Count).ShouldBe(251);
            summary.Bins[^1].CumulativeShare.ShouldBe(1.0, 1e-12);
        }

        [Fact]
        public void Analyze_Given_AResultWithoutPathsOrCriticalActivities_Then_HasOnlyTheOtherGroupAndNoActivities()
        {
            var result = new SimulationResult<int>([10], [4, 5, 5], null, [], null, null, null);

            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(result, s_Names, 5, null);

            summary.ChartPathCount.ShouldBe(0);
            summary.Paths.ShouldBeEmpty();
            summary.Activities.ShouldBeEmpty();
            summary.Bins.Select(x => x.PathGroupWeights.Single()).ShouldBe([1.0, 2.0], 1e-12);
        }

        [Fact]
        public void Analyze_Given_NoValidIteration_Then_HasNoResult()
        {
            var result = new SimulationResult<int>([10], [-1, -1], new ulong[2], [], null, null, null);

            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(result, s_Names, 5, targetFinishTime: 7);

            summary.HasResult.ShouldBeFalse();
            summary.IterationCount.ShouldBe(2);
            summary.FailedIterationCount.ShouldBe(2);
            summary.Bins.ShouldBeEmpty();
            summary.Percentiles.ShouldBeEmpty();
            summary.TargetProbability.ShouldBeNull();
        }

        [Fact]
        public void Analyze_Given_TheSameResultTwice_Then_GivesTheSameSummary()
        {
            SimulationSummaryModel first = SimulationAnalyzer.Analyze(TenIterations(), s_Names, 11, 12);
            SimulationSummaryModel second = SimulationAnalyzer.Analyze(TenIterations(), s_Names, 11, 12);

            second.Mean.ShouldBe(first.Mean);
            second.Paths.Select(x => (x.Label, x.Weight)).ShouldBe(first.Paths.Select(x => (x.Label, x.Weight)));
            second.Bins.Select(x => x.PathGroupWeights.ToArray()).ShouldBe(first.Bins.Select(x => x.PathGroupWeights.ToArray()));
        }
    }
}
