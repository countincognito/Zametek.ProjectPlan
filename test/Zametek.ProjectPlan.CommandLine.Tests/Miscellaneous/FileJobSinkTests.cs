using Shouldly;
using Xunit;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp's end of a job's messages: each is printed on the console
    /// zpp gives the sink, as the desktop would show it in a dialog.
    /// </summary>
    public class FileJobSinkTests
    {
        [Fact]
        public async Task ReportAsync_Given_AnError_Then_PrintsItOnTheConsolesStderr()
        {
            var console = new RecordingJobConsole();
            var sink = new FileJobSink(new Dictionary<JobOutput, string>(), console);

            await sink.ReportAsync(new JobMessage(JobMessageKind.Error, @"Error", string.Empty, @"The chart could not be saved"));

            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(@"Error: The chart could not be saved"));
        }

        [Fact]
        public async Task ReportAsync_Given_Information_Then_PrintsItOnTheConsolesStdout()
        {
            var console = new RecordingJobConsole();
            var sink = new FileJobSink(new Dictionary<JobOutput, string>(), console);

            await sink.ReportAsync(new JobMessage(JobMessageKind.Information, @"Information", string.Empty, @"The plan was saved"));

            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.Line(@"Information: The plan was saved"));
        }
    }
}
