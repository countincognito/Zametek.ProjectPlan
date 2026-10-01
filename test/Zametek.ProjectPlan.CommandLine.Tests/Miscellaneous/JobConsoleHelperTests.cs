using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
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
    }
}
