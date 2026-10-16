using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class SimulationCsvWriterTests
    {
        private static readonly IReadOnlyDictionary<int, string> s_Names = new Dictionary<int, string>
        {
            [10] = "A",
            [20] = "Fit, test",
            [30] = "The \"big\" one",
        };

        private static SimulationResult<int> Result() =>
            new(
                [10, 20, 30],
                [10, -1, 12, 12],
                [0b011, 0, 0b101, 0b101],
                [
                    new CriticalPath<int>([new CriticalPathStep<int>(10, CriticalPathLink.None), new CriticalPathStep<int>(20, CriticalPathLink.Dependency)]),
                    new CriticalPath<int>([new CriticalPathStep<int>(10, CriticalPathLink.None), new CriticalPathStep<int>(30, CriticalPathLink.Resource)]),
                ],
                [0, 1, 1, 2, 3],
                [0, 1, 1],
                [true, true, true, true]);

        private static string Write(System.Action<TextWriter> write)
        {
            using var writer = new StringWriter();
            write(writer);
            return writer.ToString();
        }

        private static string[] Lines(string text) => text.TrimEnd('\n').Split('\n');

        [Fact]
        public void WriteStatistics_Given_ASummary_Then_HasARowForEachStatistic()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(Result(), s_Names, plannedFinishTime: 11, targetFinishTime: 11);

            string[] lines = Lines(Write(w => SimulationCsvWriter.WriteStatistics(w, summary)));

            lines[0].ShouldBe("Statistic,Value");
            lines.ShouldContain("Iterations,4");
            lines.ShouldContain("ValidIterations,3");
            lines.ShouldContain("FailedIterations,1");
            lines.ShouldContain("PlannedFinish,11");
            lines.ShouldContain("Minimum,10");
            lines.ShouldContain("Maximum,12");
            lines.ShouldContain("P50,12");
            lines.ShouldContain("TargetFinish,11");
            lines.ShouldContain($@"ProbabilityOfFinishingByTarget,{1.0 / 3.0:R}");
        }

        [Fact]
        public void WriteStatistics_Given_NoValidIteration_Then_HasOnlyTheCounts()
        {
            var result = new SimulationResult<int>([10], [-1], null, [], null, null, null);
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(result, s_Names, 11, null);

            string[] lines = Lines(Write(w => SimulationCsvWriter.WriteStatistics(w, summary)));

            lines.ShouldBe(["Statistic,Value", "Iterations,1", "ValidIterations,0", "FailedIterations,1", "PlannedFinish,11"]);
        }

        [Fact]
        public void WriteHistogram_Given_ASummary_Then_HasABinForEachRowAndAColumnForEachGroup()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(Result(), s_Names, 11, null);

            string[] lines = Lines(Write(w => SimulationCsvWriter.WriteHistogram(w, summary)));

            lines[0].ShouldBe("FirstFinishTime,LastFinishTime,Count,Share,CumulativeShare,Path1,Path2,OtherPaths");
            lines.Length.ShouldBe(4);
            lines[1].ShouldStartWith("10,10,1,");
            lines[3].ShouldStartWith("12,12,2,");
            lines[1].Split(',').Length.ShouldBe(8);
        }

        [Fact]
        public void WritePaths_Given_ASummary_Then_TheLabelsWithACommaAreQuoted()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(Result(), s_Names, 11, null);

            string[] lines = Lines(Write(w => SimulationCsvWriter.WritePaths(w, summary)));

            lines[0].ShouldBe("Rank,Path,HasResourceLinks,Iterations,Share");
            lines[1].ShouldBe("1,\"A ~> The \"\"big\"\" one\",true,2,0.6666666666666666");
            lines[2].ShouldBe("2,\"A > Fit, test\",false,1,0.3333333333333333");
        }

        [Fact]
        public void WriteActivities_Given_ASummary_Then_HasARowForEachCriticalActivity()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(Result(), s_Names, 11, null);

            string[] lines = Lines(Write(w => SimulationCsvWriter.WriteActivities(w, summary)));

            lines[0].ShouldBe("ActivityId,Name,CriticalIterations,CriticalityIndex");
            lines[1].ShouldBe("10,A,3,1");
            lines[2].ShouldBe("30,\"The \"\"big\"\" one\",2,0.6666666666666666");
            lines[3].ShouldBe("20,\"Fit, test\",1,0.3333333333333333");
        }

        [Fact]
        public void WriteIterations_Given_AResult_Then_HasARowForEachIterationAndAFailedOneHasNoFinishTime()
        {
            string[] lines = Lines(Write(w => SimulationCsvWriter.WriteIterations(w, Result(), s_Names)));

            lines.ShouldBe(
            [
                "Iteration,FinishTime,CriticalPaths",
                "1,10,\"A > Fit, test\"",
                "2,,",
                "3,12,\"A ~> The \"\"big\"\" one\"",
                "4,12,\"A ~> The \"\"big\"\" one\"",
            ]);
        }

        [Fact]
        public void WriteIterations_Given_AResultWithTiedPaths_Then_SeparatesThemWithABar()
        {
            var result = new SimulationResult<int>(
                [10, 20, 30],
                [9],
                null,
                [
                    new CriticalPath<int>([new CriticalPathStep<int>(10, CriticalPathLink.None)]),
                    new CriticalPath<int>([new CriticalPathStep<int>(20, CriticalPathLink.None)]),
                ],
                [0, 2],
                [0, 1],
                [true]);

            string[] lines = Lines(Write(w => SimulationCsvWriter.WriteIterations(w, result, s_Names)));

            lines[1].ShouldBe("1,9,\"A | Fit, test\"");
        }

        [Fact]
        public void Write_Given_AnyCsv_Then_EveryLineEndsWithALineFeedAndNoCarriageReturn()
        {
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(Result(), s_Names, 11, 11);

            string text = Write(w =>
            {
                SimulationCsvWriter.WriteStatistics(w, summary);
                SimulationCsvWriter.WriteHistogram(w, summary);
                SimulationCsvWriter.WritePaths(w, summary);
                SimulationCsvWriter.WriteActivities(w, summary);
                SimulationCsvWriter.WriteIterations(w, Result(), s_Names);
            });

            text.ShouldNotContain("\r");
            text.ShouldEndWith("\n");
        }
    }
}
