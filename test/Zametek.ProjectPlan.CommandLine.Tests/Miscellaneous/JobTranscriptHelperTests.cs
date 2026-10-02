using Shouldly;
using System.Text;
using Xunit;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for how zpp plays back the transcript of a job a server ran: what
    /// it refuses to play - a transcript that does not match the job zpp sent -
    /// and how it plays the rest: each thing printed and each output written, in
    /// the order the job did them, an error's line ends as the platform ends
    /// them, and a file that cannot be written failing the run as it fails a job
    /// here - a chart or a graph reported and the run carried on, the project
    /// or the export stopping it.
    /// </summary>
    public class JobTranscriptHelperTests
    {
        // The outputs the job asked for.
        private static readonly Dictionary<JobOutput, string> s_Filenames = new()
        {
            [JobOutput.Project] = @"saved.zpp",
            [JobOutput.GanttChart] = @"plan-gantt.png",
            [JobOutput.ArrowGraph] = @"plan-arrow.svg",
        };

        private static readonly JobResponseOutput[] s_Outputs =
        [
            Output(JobOutput.Project, @"project"),
            Output(JobOutput.GanttChart, @"gantt"),
            Output(JobOutput.ArrowGraph, @"arrow"),
        ];

        private static JobResponseOutput Output(JobOutput kind, string content)
        {
            return new JobResponseOutput(kind, s_Filenames[kind], @"application/octet-stream", Encoding.UTF8.GetBytes(content));
        }

        private static JobTranscriptEntry Line(string text) => new() { Kind = JobTranscriptKind.Line, Text = text };

        private static JobTranscriptEntry Display(string text, bool hasErrors = false) => new() { Kind = JobTranscriptKind.Display, Text = text, HasErrors = hasErrors };

        private static JobTranscriptEntry ErrorLine(string text) => new() { Kind = JobTranscriptKind.ErrorLine, Text = text };

        private static JobTranscriptEntry Produced(int index) => new() { Kind = JobTranscriptKind.Output, Index = index };

        // Each call on the console and each output given to the sink, in one list, in the order they came; and the
        // outputs it fails to write, as a file that cannot be written fails.
        private sealed class Recorder(params JobOutput[] failing)
            : IJobConsole, IJobSink
        {
            public List<string> Calls { get; } = [];

            public Task WriteLineAsync(string text)
            {
                Calls.Add($@"line: {text}");
                return Task.CompletedTask;
            }

            public Task DisplayAsync(string content, bool hasErrors)
            {
                Calls.Add($@"display{(hasErrors ? @" with errors" : string.Empty)}: {content}");
                return Task.CompletedTask;
            }

            public Task WriteErrorLineAsync(string text)
            {
                Calls.Add($@"error: {text}");
                return Task.CompletedTask;
            }

            public async Task WriteOutputAsync(JobOutput output, Func<Stream, Task> write)
            {
                if (failing.Contains(output))
                {
                    throw new IOException($@"{output} is in use");
                }

                using var stream = new MemoryStream();
                await write(stream);
                Calls.Add($@"{output}: {Encoding.UTF8.GetString(stream.ToArray())}");
            }

            public Task ReportAsync(JobMessage message)
            {
                Calls.Add($@"reported {message.Kind}: {message.Title}: {message.Message}");
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void IsPlayable_Given_ATranscriptOfEveryKind_Then_True()
        {
            JobTranscriptHelper.IsPlayable(
                [Line(@"one"), Produced(0), ErrorLine(@"two"), Produced(2), Display(@"three")],
                s_Outputs,
                s_Filenames).ShouldBeTrue();
        }

        [Fact]
        public void IsPlayable_Given_NoTranscriptOrNoOutputs_Then_False()
        {
            JobTranscriptHelper.IsPlayable(null, s_Outputs, s_Filenames).ShouldBeFalse();
            JobTranscriptHelper.IsPlayable([Line(@"one")], null, s_Filenames).ShouldBeFalse();
        }

        public static TheoryData<JobTranscriptEntry> EntriesThatCannotBePlayed => new()
        {
            new JobTranscriptEntry { Kind = JobTranscriptKind.Line },
            new JobTranscriptEntry { Kind = JobTranscriptKind.Display },
            new JobTranscriptEntry { Kind = JobTranscriptKind.ErrorLine },
            new JobTranscriptEntry { Kind = JobTranscriptKind.Output },
            Produced(-1),
            Produced(3),
            new JobTranscriptEntry { Kind = (JobTranscriptKind)99, Text = @"what" },
        };

        [Theory]
        [MemberData(nameof(EntriesThatCannotBePlayed))]
        public void IsPlayable_Given_AnEntryItCannotPlay_Then_False(JobTranscriptEntry entry)
        {
            // A line or a block with no text, an output it does not name or the answer does not have, and an entry of a
            // kind zpp does not know.
            JobTranscriptHelper.IsPlayable([Line(@"one"), entry], s_Outputs, s_Filenames).ShouldBeFalse();
        }

        [Fact]
        public void IsPlayable_Given_AnOutputWithoutItsContent_Then_False()
        {
            JobTranscriptHelper.IsPlayable([Produced(0)], [s_Outputs[0] with { Content = null }], s_Filenames).ShouldBeFalse();
        }

        [Fact]
        public void IsPlayable_Given_AnOutputTheJobDidNotAskFor_Then_False()
        {
            JobTranscriptHelper.IsPlayable([Produced(0)], [Output(JobOutput.Project, @"project")], new Dictionary<JobOutput, string>()).ShouldBeFalse();
        }

        [Fact]
        public async Task PlayAsync_Given_EachKindOfEntry_Then_PrintsAndWritesThemInOrder()
        {
            var recorder = new Recorder();

            ExitCode exitCode = await JobTranscriptHelper.PlayAsync(
                [Line(@"one"), Produced(0), ErrorLine(@"two"), Produced(1), Display(@"three"), Produced(2), Display(@"four", hasErrors: true)],
                s_Outputs,
                ExitCode.Success,
                recorder,
                recorder);

            exitCode.ShouldBe(ExitCode.Success);
            recorder.Calls.ShouldBe(
            [
                @"line: one",
                @"Project: project",
                @"error: two",
                @"GanttChart: gantt",
                @"display: three",
                @"ArrowGraph: arrow",
                @"display with errors: four",
            ]);
        }

        [Fact]
        public async Task PlayAsync_Given_AnErrorWithLineEnds_Then_EndsThemAsThePlatformDoes()
        {
            var recorder = new Recorder();

            await JobTranscriptHelper.PlayAsync([ErrorLine("first\nsecond")], [], ExitCode.Failure, recorder, recorder);

            recorder.Calls.ShouldBe([$@"error: first{Environment.NewLine}second"]);
        }

        [Theory]
        [InlineData(ExitCode.Success, ExitCode.Failure)]
        [InlineData(ExitCode.Failure, ExitCode.Failure)]
        public async Task PlayAsync_Given_AChartThatCannotBeWritten_Then_ReportsItCarriesOnAndFails(ExitCode jobsExitCode, ExitCode exitCode)
        {
            var recorder = new Recorder(JobOutput.GanttChart);

            (await JobTranscriptHelper.PlayAsync([Produced(1), Produced(2), Display(@"metrics")], s_Outputs, jobsExitCode, recorder, recorder))
                .ShouldBe(exitCode);

            recorder.Calls.ShouldBe(
            [
                $@"reported {JobMessageKind.Error}: {Resource.ProjectPlan.Titles.Title_Error}: GanttChart is in use",
                @"ArrowGraph: arrow",
                @"display: metrics",
            ]);
        }

        [Fact]
        public async Task PlayAsync_Given_AProjectThatCannotBeWritten_Then_ThrowsAndPlaysNoMore()
        {
            var recorder = new Recorder(JobOutput.Project);

            (await Should.ThrowAsync<IOException>(() => JobTranscriptHelper.PlayAsync(
                [Line(@"one"), Produced(0), Produced(1), Display(@"metrics")],
                s_Outputs,
                ExitCode.Success,
                recorder,
                recorder))).Message.ShouldBe(@"Project is in use");

            recorder.Calls.ShouldBe([@"line: one"]);
        }

        [Theory]
        [InlineData(ExitCode.Success)]
        [InlineData(ExitCode.Failure)]
        [InlineData(ExitCode.CompilationErrors)]
        [InlineData(ExitCode.CompilationTimeout)]
        public async Task PlayAsync_Given_NothingFails_Then_TheJobsExitCode(ExitCode exitCode)
        {
            var recorder = new Recorder();

            (await JobTranscriptHelper.PlayAsync([Display(@"block", hasErrors: true)], [], exitCode, recorder, recorder)).ShouldBe(exitCode);
        }
    }
}
