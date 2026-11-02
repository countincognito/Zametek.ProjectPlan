using Shouldly;
using System.Net;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;
using Zametek.ViewModel.ProjectPlan.Tests;
using static Zametek.ViewModel.ProjectPlan.Tests.XlsxCellSpec;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp serve's answer to a workbook that has problems in its cells: the import is cancelled, and the problem that
    /// the server answers with lists each of them, with the place of each, up to a limit that it says.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class ProjectImportProblemsTests
        : IClassFixture<EngineFixture>, IAsyncLifetime
    {
        private const int c_Limit = 100;

        private static readonly string s_Activities = Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities;

        private readonly EngineFixture m_Engine;
        private RunningServer? m_Server;

        public ProjectImportProblemsTests(EngineFixture engine)
        {
            m_Engine = engine;
        }

        public async Task InitializeAsync()
        {
            m_Server = await RunningServer.StartAsync(m_Engine.JobRunner);
        }

        public async Task DisposeAsync()
        {
            if (m_Server is not null)
            {
                await m_Server.DisposeAsync();
            }
        }

        // A workbook of activities, each with a Duration that is not a number.
        private static byte[] WorkbookWithProblems(int count) =>
            new XlsxPackageBuilder()
                .Sheet(s_Activities, s =>
                {
                    s.Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))), ("C", TitleOf(nameof(ActivityModel.Duration))));

                    for (int i = 0; i < count; i++)
                    {
                        s.Row(i + 2, ("A", NumberOf(i + 1)), ("B", TextOf($@"Task {i + 1}")), ("C", TextOf(@"soon")));
                    }
                })
                .Build();

        private async Task<(HttpResponseMessage Response, ProblemResponse Problem)> CompileAsync(byte[] workbook)
        {
            using var content = RunningServer.CompileContent(workbook, @"plan.xlsx", null, @"import");
            HttpResponseMessage response = await m_Server!.Client.PostAsync(@"/v1/projects/compile?include=console", content);
            return (response, await RunningServer.ReadProblemAsync(response));
        }

        [Fact]
        public async Task Compile_Given_AWorkbookWithProblems_Then_UnprocessableWithAnErrorForEachProblem()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(WorkbookWithProblems(3));
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.ProjectNotReadable));
            problem.Detail.ShouldBe(ImportIssueText.BuildHeader(3));

            // Each problem is an error at the import, with its kind as the code and its place in the words.
            problem.Errors.ShouldNotBeNull().Select(x => (x.Pointer, x.Code, x.Detail)).ShouldBe(
            [
                (@"#/import", ImportIssueCodes.NotAWholeNumber, @"Activities!C2 (Duration, Task 1): ""soon"" is not a whole number."),
                (@"#/import", ImportIssueCodes.NotAWholeNumber, @"Activities!C3 (Duration, Task 2): ""soon"" is not a whole number."),
                (@"#/import", ImportIssueCodes.NotAWholeNumber, @"Activities!C4 (Duration, Task 3): ""soon"" is not a whole number."),
            ]);

            // What zpp prints, and the exit code that it ends with.
            ConsoleResponse console = problem.Console.ShouldNotBeNull();
            console.ExitCode.ShouldBe((int)ExitCode.Failure);
            console.StandardOutput.ShouldBeEmpty();
            console.StandardError.TrimEnd().Split('\n').ShouldBe(
            [
                ImportIssueText.BuildHeader(3),
                @"Activities!C2 (Duration, Task 1): ""soon"" is not a whole number.",
                @"Activities!C3 (Duration, Task 2): ""soon"" is not a whole number.",
                @"Activities!C4 (Duration, Task 3): ""soon"" is not a whole number.",
            ]);
            console.Transcript.Count.ShouldBe(4);
            console.Transcript.ShouldAllBe(x => x.Kind == JobTranscriptKind.ErrorLine);
        }

        // A workbook can have thousands of problems, and a response lists the first of them and says how many there are.
        [Fact]
        public async Task Compile_Given_ManyProblems_Then_TheErrorsAreTheFirstHundredAndTheDetailSaysSo()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(WorkbookWithProblems(c_Limit + 50));
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            problem.Detail.ShouldBe($@"{ImportIssueText.BuildHeader(c_Limit + 50)} {ImportIssueText.BuildListed(c_Limit)}");

            IReadOnlyList<ProblemError> errors = problem.Errors.ShouldNotBeNull();
            errors.Count.ShouldBe(c_Limit);
            errors[0].Detail.ShouldStartWith(@"Activities!C2 ");
            errors[^1].Detail.ShouldStartWith($@"Activities!C{c_Limit + 1} ");

            string[] lines = problem.Console.ShouldNotBeNull().StandardError.TrimEnd().Split('\n');
            lines.Length.ShouldBe(c_Limit + 2);
            lines[^1].ShouldBe(@"And 50 more.");
        }

        // A file that is not a workbook at all is the one problem that is not in a cell, and the answer to it is the one that it
        // has always been (see ProjectProblemsTests).
        [Fact]
        public async Task Compile_Given_AFileThatIsNotAWorkbook_Then_TheErrorIsUnreadableAndNotAProblemOfACell()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(System.Text.Encoding.UTF8.GetBytes(@"This is not a workbook."));
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            problem.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeWorkbookNotReadable);
            problem.Errors.ShouldNotBeNull().Select(x => (x.Pointer, x.Code)).ShouldBe([(@"#/import", @"unreadable")]);
        }
    }
}
