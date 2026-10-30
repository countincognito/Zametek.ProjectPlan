using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the text zpp prints for a run and the exit code the run ends
    /// with: what goes to stdout and what to stderr, which blocks are displayed
    /// and which of them report errors, and the wording of a scenario the engine
    /// could not select (the engine's own ScenarioSelectorTests cover the
    /// selecting). Anything else that prints a job's text through the same
    /// helper says what zpp would.
    /// </summary>
    public class JobConsoleHelperTests
    {
        private static readonly JobSimulationMetrics s_Simulation = new()
        {
            Iterations = 5000,
            Seed = 18_446_744_073_709_551_615UL,
            Distribution = DurationDistribution.ModifiedBetaPert,
            ModifiedBetaShape = 6.5,
            ValidIterations = 4990,
            FailedIterations = 10,
            PlannedFinishDays = 26,
            PlannedFinishDate = new DateOnly(2024, 1, 27),
            MeanDays = 26.4,
            StandardDeviationDays = 3.1,
            EarliestFinishDays = 17,
            EarliestFinishDate = new DateOnly(2024, 1, 18),
            LatestFinishDays = 41,
            LatestFinishDate = new DateOnly(2024, 2, 11),
            Percentiles = [new JobSimulationPercentile(50, 26, new DateOnly(2024, 1, 27)), new JobSimulationPercentile(80, 29, new DateOnly(2024, 1, 30))],
            TargetFinishDays = 30,
            TargetFinishDate = new DateOnly(2024, 1, 31),
            TargetProbability = 0.86,
            Statistics =
            [
                new SimulationStatisticModel { Name = @"Seed", Value = @"18446744073709551615" },
                new SimulationStatisticModel { Name = @"Mean", Value = @"26.4" },
                new SimulationStatisticModel { Name = @"P80", Value = @"29" },
            ],
        };

        private static readonly JobMetrics s_Metrics = new()
        {
            ActivityRisk = 0.25,
            NetworkCyclomaticComplexity = 3,
            ProjectFinish = @"Friday",
            TotalCost = 1234.5,
            TotalMarginAbsolute = 100,
            DisplayTotalMargin = @" (10%)",
        };

        [Fact]
        public async Task WriteResultAsync_Given_CompilationErrors_Then_DisplaysTheCompilerOutputAsErrors()
        {
            var console = new RecordingJobConsole();

            ExitCode exitCode = await JobConsoleHelper.WriteResultAsync(
                console,
                new JobResult { Status = JobStatus.CompilationErrors, CompilationOutput = @"Activity 2 has an invalid dependency" },
                MetricsExport.Json);

            exitCode.ShouldBe(ExitCode.CompilationErrors);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.Display(@"Activity 2 has an invalid dependency", hasErrors: true));
        }

        [Fact]
        public async Task WriteResultAsync_Given_JsonMetrics_Then_WritesTheJsonAlone()
        {
            var console = new RecordingJobConsole();

            ExitCode exitCode = await JobConsoleHelper.WriteResultAsync(
                console,
                new JobResult { Status = JobStatus.Succeeded, Metrics = s_Metrics },
                MetricsExport.Json);

            exitCode.ShouldBe(ExitCode.Success);
            RecordingJobConsole.Call call = console.Calls.ShouldHaveSingleItem();
            call.ShouldBe(RecordingJobConsole.Line(JobConsoleHelper.BuildMetricsJson(s_Metrics)));
            JObject metrics = JObject.Parse(call.Text);
            metrics[@"TotalCost"]!.Value<double>().ShouldBe(1234.5);
            metrics[@"ProjectFinish"]!.Value<string>().ShouldBe(@"Friday");
        }

        [Fact]
        public async Task WriteResultAsync_Given_TableMetrics_Then_DisplaysTheTable()
        {
            var console = new RecordingJobConsole();

            ExitCode exitCode = await JobConsoleHelper.WriteResultAsync(
                console,
                new JobResult { Status = JobStatus.Succeeded, Metrics = s_Metrics },
                MetricsExport.Table);

            exitCode.ShouldBe(ExitCode.Success);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.Display(JobConsoleHelper.BuildMetricsTable(s_Metrics).ToString(), hasErrors: false));
        }

        [Fact]
        public async Task WriteResultAsync_Given_MarkdownMetrics_Then_DisplaysTheTableAsMarkdown()
        {
            var console = new RecordingJobConsole();

            ExitCode exitCode = await JobConsoleHelper.WriteResultAsync(
                console,
                new JobResult { Status = JobStatus.Succeeded, Metrics = s_Metrics },
                MetricsExport.Markdown);

            exitCode.ShouldBe(ExitCode.Success);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.Display(JobConsoleHelper.BuildMetricsTable(s_Metrics).ToMarkDownString(), hasErrors: false));
        }

        [Fact]
        public async Task WriteResultAsync_Given_ASimulationAndJsonMetrics_Then_WritesTheJsonWithItsStatistics()
        {
            var console = new RecordingJobConsole();

            ExitCode exitCode = await JobConsoleHelper.WriteResultAsync(
                console,
                new JobResult { Status = JobStatus.Succeeded, Metrics = s_Metrics, Simulation = s_Simulation },
                MetricsExport.Json);

            exitCode.ShouldBe(ExitCode.Success);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.Line(JobConsoleHelper.BuildMetricsJson(s_Metrics, s_Simulation)));
        }

        [Theory]
        [InlineData(MetricsExport.Markdown)]
        [InlineData(MetricsExport.Table)]
        public async Task WriteResultAsync_Given_ASimulation_Then_ItsStatisticsAreATableAfterTheTableOfTheMetrics(MetricsExport format)
        {
            string without = await DisplayedAsync(new JobResult { Status = JobStatus.Succeeded, Metrics = s_Metrics }, format);
            string with = await DisplayedAsync(new JobResult { Status = JobStatus.Succeeded, Metrics = s_Metrics, Simulation = s_Simulation }, format);

            // The table of the metrics is the table of a run without a simulation, and one blank line sets the next table apart.
            with.ShouldStartWith(without + NewLineHelper.NewLine);
            string statistics = with[(without.Length + NewLineHelper.NewLine.Length)..];
            statistics.ShouldNotStartWith(NewLineHelper.NewLine);

            // The statistics are the rows of that table, as the panel shows them, under a heading of their own.
            Rows(statistics).ShouldBe(
            [
                $@"{Resource.ProjectPlan.Titles.Title_SimulationMetrics} | {Resource.ProjectPlan.Titles.Title_Values}",
                @"Seed | 18446744073709551615",
                @"Mean | 26.4",
                @"P80 | 29",
            ]);
        }

        [Theory]
        [InlineData(MetricsExport.Markdown)]
        [InlineData(MetricsExport.Table)]
        public async Task WriteResultAsync_Given_NoSimulation_Then_TheMetricsAreTheOnlyTable(MetricsExport format)
        {
            string text = await DisplayedAsync(new JobResult { Status = JobStatus.Succeeded, Metrics = s_Metrics }, format);

            Rows(text).First().ShouldBe($@"{Resource.ProjectPlan.Titles.Title_Metrics} | {Resource.ProjectPlan.Titles.Title_Values}");
            Rows(text).ShouldNotContain(x => x.StartsWith(Resource.ProjectPlan.Titles.Title_SimulationMetrics, StringComparison.Ordinal));

            // Nothing follows the table: no other table, and no count of its rows, which the table library adds by default.
            text.ShouldNotContain(NewLineHelper.NewLine + NewLineHelper.NewLine);
            text.ShouldNotContain(@"Count");
        }

        [Theory]
        [InlineData(MetricsExport.Markdown)]
        [InlineData(MetricsExport.Table)]
        public async Task WriteResultAsync_Given_AStatisticOfMoreThanFortyCharacters_Then_ItStaysInOneRow(MetricsExport format)
        {
            // The longest statistic of the panel: the chance of the target, with the target and its date. The table library
            // breaks a cell of more than 40 characters, and then Markdown reads the rest as a row of its own.
            const string name = @"Chance of finishing by the target (96 (18/05/2027))";
            JobSimulationMetrics simulation = s_Simulation with { Statistics = [new SimulationStatisticModel { Name = name, Value = @"70.0%" }] };

            string text = await DisplayedAsync(new JobResult { Status = JobStatus.Succeeded, Metrics = s_Metrics, Simulation = simulation }, format);

            Rows(text).ShouldContain($@"{name} | 70.0%");
        }

        [Theory]
        [InlineData(MetricsExport.Markdown)]
        [InlineData(MetricsExport.Table)]
        public async Task WriteResultAsync_Given_AMetricOfMoreThanFortyCharacters_Then_ItStaysInOneRow(MetricsExport format)
        {
            string finish = string.Join(' ', Enumerable.Repeat(@"Friday", 10));
            JobMetrics metrics = s_Metrics with { ProjectFinish = finish };

            string text = await DisplayedAsync(new JobResult { Status = JobStatus.Succeeded, Metrics = metrics }, format);

            Rows(text).ShouldContain($@"{Resource.ProjectPlan.Labels.Label_ProjectFinish} | {finish}");
        }

        [Fact]
        public void BuildMetricsJson_Given_NoSimulation_Then_TheTextItWasBeforeThereWasOne()
        {
            JobConsoleHelper.BuildMetricsJson(s_Metrics, null).ShouldBe(JobConsoleHelper.BuildMetricsJson(s_Metrics));
            JObject.Parse(JobConsoleHelper.BuildMetricsJson(s_Metrics)).ContainsKey(@"Simulation").ShouldBeFalse();
        }

        [Fact]
        public void BuildMetricsJson_Given_ASimulation_Then_ItsValuesFollowTheMetricsInAnObjectOfTheirOwn()
        {
            string json = JobConsoleHelper.BuildMetricsJson(s_Metrics, s_Simulation);
            JObject all = JObject.Parse(json);

            // What the metrics were is what they are, and the simulation comes after them.
            all.Properties().Select(x => x.Name).Last().ShouldBe(@"Simulation");
            JobConsoleHelper.BuildMetricsJson(s_Metrics).ShouldNotBe(json);
            all[@"TotalCost"]!.Value<double>().ShouldBe(1234.5);

            var simulation = (JObject)all[@"Simulation"]!;
            simulation.Properties().Select(x => x.Name).ShouldBe(
            [
                @"Iterations", @"ValidIterations", @"FailedIterations", @"Seed", @"Distribution", @"ModifiedBetaShape",
                @"PlannedFinishDays", @"PlannedFinishDate", @"MeanDays", @"StandardDeviationDays",
                @"EarliestFinishDays", @"EarliestFinishDate", @"LatestFinishDays", @"LatestFinishDate",
                @"Percentiles", @"TargetFinishDays", @"TargetFinishDate", @"TargetProbability",
            ]);

            // A seed can be larger than every reader reads without loss, and so it is text.
            simulation[@"Seed"]!.Type.ShouldBe(JTokenType.String);
            simulation[@"Seed"]!.Value<string>().ShouldBe(@"18446744073709551615");
            simulation[@"Distribution"]!.Value<string>().ShouldBe(@"ModifiedBetaPert");
            simulation[@"ModifiedBetaShape"]!.Value<double>().ShouldBe(6.5);
            simulation[@"Iterations"]!.Value<int>().ShouldBe(5000);
            simulation[@"ValidIterations"]!.Value<int>().ShouldBe(4990);
            simulation[@"PlannedFinishDate"]!.Type.ShouldBe(JTokenType.String);
            simulation[@"PlannedFinishDate"]!.Value<string>().ShouldBe(@"2024-01-27");
            simulation[@"MeanDays"]!.Value<double>().ShouldBe(26.4);
            simulation[@"TargetProbability"]!.Value<double>().ShouldBe(0.86);
            ((JArray)simulation[@"Percentiles"]!).Select(x => x[@"Percentile"]!.Value<int>()).ShouldBe([50, 80]);
            ((JArray)simulation[@"Percentiles"]!)[1][@"FinishDate"]!.Value<string>().ShouldBe(@"2024-01-30");

            // The text ends its lines with \n, and so does the text of the metrics without a simulation.
            json.ShouldNotContain('\r');
        }

        [Fact]
        public void BuildMetricsJson_Given_ASimulationWithNoStatistics_Then_NullsAreWrittenAsTheMetricsWriteThem()
        {
            var empty = new JobSimulationMetrics { Iterations = 10, ValidIterations = 0, FailedIterations = 10, PlannedFinishDate = new DateOnly(2024, 1, 6) };

            var simulation = (JObject)JObject.Parse(JobConsoleHelper.BuildMetricsJson(s_Metrics, empty))[@"Simulation"]!;

            simulation[@"MeanDays"]!.Type.ShouldBe(JTokenType.Null);
            simulation[@"EarliestFinishDate"]!.Type.ShouldBe(JTokenType.Null);
            simulation[@"TargetProbability"]!.Type.ShouldBe(JTokenType.Null);
            ((JArray)simulation[@"Percentiles"]!).ShouldBeEmpty();
        }

        [Fact]
        public async Task WriteResultAsync_Given_AJobThatCompletedWithErrors_Then_PrintsTheMetricsAndFails()
        {
            // The errors themselves were printed as the job reported them.
            var console = new RecordingJobConsole();

            ExitCode exitCode = await JobConsoleHelper.WriteResultAsync(
                console,
                new JobResult { Status = JobStatus.CompletedWithErrors, Metrics = s_Metrics },
                MetricsExport.Json);

            exitCode.ShouldBe(ExitCode.Failure);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.Line(JobConsoleHelper.BuildMetricsJson(s_Metrics)));
        }

        [Fact]
        public async Task WriteFailureAsync_Given_AScenarioTheJobCouldNotSelect_Then_SaysWhyInZppsWordsAndFails()
        {
            var console = new RecordingJobConsole();
            var ex = new ScenarioSelectionException(@"No scenario matches 'Gamma'", @"Gamma", ScenarioSelectionFailure.NoMatch, 0);

            ExitCode exitCode = await JobConsoleHelper.WriteFailureAsync(console, ex);

            exitCode.ShouldBe(ExitCode.Failure);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(
                string.Format(Resource.ProjectPlan.Messages.Message_NoScenarioMatches, @"Gamma", @"--list-scenarios")));
        }

        [Fact]
        public async Task WriteFailureAsync_Given_ACompilationThatRanOutOfTime_Then_SaysSoAndExitsWithTheTimeoutCode()
        {
            var console = new RecordingJobConsole();

            ExitCode exitCode = await JobConsoleHelper.WriteFailureAsync(console, new GraphCompilationTimeoutException(@"Compilation timed out"));

            exitCode.ShouldBe(ExitCode.CompilationTimeout);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(@"Compilation timed out"));
        }

        [Fact]
        public async Task WriteFailureAsync_Given_AnyOtherException_Then_PrintsItsMessageAndFails()
        {
            var console = new RecordingJobConsole();

            ExitCode exitCode = await JobConsoleHelper.WriteFailureAsync(console, new IOException(@"Disk full"));

            exitCode.ShouldBe(ExitCode.Failure);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(@"Disk full"));
        }

        [Fact]
        public async Task WriteScenariosAsync_Given_Scenarios_Then_DisplaysARowForEach()
        {
            var console = new RecordingJobConsole();
            var alphaId = Guid.NewGuid();
            var betaId = Guid.NewGuid();
            ScenarioSummary[] scenarios =
            [
                new(@"Alpha", alphaId, IsTracked: true, IsCurrent: false),
                new(@"Folder/Beta", betaId, IsTracked: false, IsCurrent: true),
            ];

            await JobConsoleHelper.WriteScenariosAsync(console, scenarios);

            RecordingJobConsole.Call call = console.Calls.ShouldHaveSingleItem();
            call.ShouldBe(RecordingJobConsole.Display(JobConsoleHelper.BuildScenarioTable(scenarios).ToMarkDownString(), hasErrors: false));

            string[] lines = call.Text.Split(NewLineHelper.LineFeed);
            string alpha = lines.Where(x => x.Contains(alphaId.ToString(), StringComparison.Ordinal)).ShouldHaveSingleItem();
            alpha.ShouldContain(@"Alpha");
            alpha.ShouldContain(Resource.ProjectPlan.Labels.Label_Yes);
            alpha.ShouldNotContain(Resource.ProjectPlan.Symbols.Symbol_Current);
            string beta = lines.Where(x => x.Contains(betaId.ToString(), StringComparison.Ordinal)).ShouldHaveSingleItem();
            beta.ShouldContain(@"Folder/Beta");
            beta.ShouldNotContain(Resource.ProjectPlan.Labels.Label_Yes);
            beta.ShouldContain(Resource.ProjectPlan.Symbols.Symbol_Current);
        }

        [Theory]
        [InlineData(JobMessageKind.Error)]
        [InlineData(JobMessageKind.Warning)]
        public async Task WriteMessageAsync_Given_AnErrorOrAWarning_Then_PrintsItOnStderr(JobMessageKind kind)
        {
            var console = new RecordingJobConsole();

            await JobConsoleHelper.WriteMessageAsync(console, new JobMessage(kind, @"Error", @"Header", @"The chart could not be saved"));

            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(@"Error: The chart could not be saved"));
        }

        [Theory]
        [InlineData(JobMessageKind.Information)]
        [InlineData(JobMessageKind.Notification)]
        public async Task WriteMessageAsync_Given_AnythingElse_Then_PrintsItOnStdout(JobMessageKind kind)
        {
            var console = new RecordingJobConsole();

            await JobConsoleHelper.WriteMessageAsync(console, new JobMessage(kind, @"Information", @"Header", @"The plan was saved"));

            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.Line(@"Information: The plan was saved"));
        }

        [Fact]
        public void BuildScenarioSelectionMessage_Given_NoMatch_Then_PointsAtListScenarios()
        {
            var ex = new ScenarioSelectionException(@"No scenario matches 'Gamma'", @"Gamma", ScenarioSelectionFailure.NoMatch, 0);

            JobConsoleHelper.BuildScenarioSelectionMessage(ex).ShouldBe(
                string.Format(Resource.ProjectPlan.Messages.Message_NoScenarioMatches, @"Gamma", @"--list-scenarios"));
        }

        [Fact]
        public void BuildScenarioSelectionMessage_Given_SeveralMatches_Then_CountsThemAndPointsAtListScenarios()
        {
            var ex = new ScenarioSelectionException(@"'Beta' matches 2 scenarios", @"Beta", ScenarioSelectionFailure.SeveralMatches, 2);

            JobConsoleHelper.BuildScenarioSelectionMessage(ex).ShouldBe(
                string.Format(Resource.ProjectPlan.Messages.Message_SeveralScenariosMatch, @"Beta", 2, @"--list-scenarios"));
        }

        [Fact]
        public void BuildScenarioSelectionMessage_Given_NoScenarioData_Then_NamesTheScenario()
        {
            var ex = new ScenarioSelectionException(@"Scenario 'Beta' has no scenario data in the project file", @"Beta", ScenarioSelectionFailure.NoScenarioData, 1);

            JobConsoleHelper.BuildScenarioSelectionMessage(ex).ShouldBe(
                string.Format(Resource.ProjectPlan.Messages.Message_ScenarioHasNoScenarioData, @"Beta"));
        }

        // The one block that a run of the job displays, with the line ends that the consoles write.
        private static async Task<string> DisplayedAsync(JobResult result, MetricsExport format)
        {
            var console = new RecordingJobConsole();

            ExitCode exitCode = await JobConsoleHelper.WriteResultAsync(console, result, format);

            exitCode.ShouldBe(ExitCode.Success);
            RecordingJobConsole.Call call = console.Calls.ShouldHaveSingleItem();
            call.ShouldBe(RecordingJobConsole.Display(call.Text, hasErrors: false));
            return NewLineHelper.NormalizeNewLines(call.Text);
        }

        // The rows of the tables in a text, in Markdown or in a box, each row as its cells with ' | ' between them. A line
        // with no cell is not a row: the rules of a box, and the line under the heading of a Markdown table.
        private static List<string> Rows(string text)
        {
            return
            [
                .. text
                    .Split(NewLineHelper.LineFeed, StringSplitOptions.RemoveEmptyEntries)
                    .Where(x => x.Contains('|'))
                    .Select(x => x.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray())
                    .Where(x => !x.All(cell => cell.Length > 0 && cell.All(character => character == '-')))
                    .Select(x => string.Join(@" | ", x)),
            ];
        }
    }
}
