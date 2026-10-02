using Shouldly;
using System.Text;
using Xunit;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp serve's end of a job: it keeps each output, in the order
    /// the job produces them, keeps nothing of an output that fails, and prints
    /// each message on the job's console as zpp prints it.
    /// </summary>
    public class MemoryJobSinkTests
    {
        private static Func<Stream, Task> Writing(string text)
        {
            return async stream => await stream.WriteAsync(Encoding.UTF8.GetBytes(text));
        }

        [Fact]
        public async Task WriteOutputAsync_Given_Outputs_Then_KeepsEachInOrder()
        {
            var sink = new MemoryJobSink(new RecordingJobConsole());

            await sink.WriteOutputAsync(JobOutput.Project, Writing(@"project"));
            await sink.WriteOutputAsync(JobOutput.GanttChart, Writing(@"gantt"));

            sink.Outputs.Select(x => (x.Output, Encoding.UTF8.GetString(x.Content)))
                .ShouldBe([(JobOutput.Project, @"project"), (JobOutput.GanttChart, @"gantt")]);
        }

        [Fact]
        public async Task WriteOutputAsync_Given_AnOutputThatFailsPartWay_Then_ThrowsAndKeepsNoneOfIt()
        {
            var sink = new MemoryJobSink(new RecordingJobConsole());

            await Should.ThrowAsync<IOException>(() => sink.WriteOutputAsync(JobOutput.GanttChart, async stream =>
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(@"half a chart"));
                throw new IOException(@"Rendering failed");
            }));

            sink.Outputs.ShouldBeEmpty();
        }

        [Fact]
        public async Task ReportAsync_Given_AnError_Then_PrintsItOnTheJobsStderr()
        {
            var console = new RecordingJobConsole();
            var sink = new MemoryJobSink(console);

            await sink.ReportAsync(new JobMessage(JobMessageKind.Error, @"Error", string.Empty, @"The chart could not be saved"));

            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(@"Error: The chart could not be saved"));
        }
    }
}
