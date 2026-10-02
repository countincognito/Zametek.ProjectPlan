using Shouldly;
using System.Text;
using Xunit;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp serve's end of a job: it keeps each output, in the order
    /// the job produces them, and records each in the job's transcript as it
    /// keeps it; keeps nothing of an output that fails; and prints each message
    /// on the job's console as zpp prints it.
    /// </summary>
    public class MemoryJobSinkTests
    {
        private static Func<Stream, Task> Writing(string text)
        {
            return async stream => await stream.WriteAsync(Encoding.UTF8.GetBytes(text));
        }

        [Fact]
        public async Task WriteOutputAsync_Given_Outputs_Then_KeepsEachInOrderAndRecordsEachInTheTranscript()
        {
            var console = new BufferedConsole();
            var sink = new MemoryJobSink(console);

            await sink.WriteOutputAsync(JobOutput.Project, Writing(@"project"));
            await console.WriteErrorLineAsync(@"between the two");
            await sink.WriteOutputAsync(JobOutput.GanttChart, Writing(@"gantt"));

            sink.Outputs.Select(x => (x.Output, Encoding.UTF8.GetString(x.Content)))
                .ShouldBe([(JobOutput.Project, @"project"), (JobOutput.GanttChart, @"gantt")]);
            console.Transcript.ShouldBe(
            [
                new JobTranscriptEntry { Kind = JobTranscriptKind.Output, Index = 0 },
                new JobTranscriptEntry { Kind = JobTranscriptKind.ErrorLine, Text = @"between the two" },
                new JobTranscriptEntry { Kind = JobTranscriptKind.Output, Index = 1 },
            ]);
        }

        [Fact]
        public async Task WriteOutputAsync_Given_AnOutputThatFailsPartWay_Then_ThrowsAndKeepsAndRecordsNoneOfIt()
        {
            var console = new BufferedConsole();
            var sink = new MemoryJobSink(console);

            await Should.ThrowAsync<IOException>(() => sink.WriteOutputAsync(JobOutput.GanttChart, async stream =>
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(@"half a chart"));
                throw new IOException(@"Rendering failed");
            }));

            sink.Outputs.ShouldBeEmpty();
            console.Transcript.ShouldBeEmpty();
        }

        [Fact]
        public async Task ReportAsync_Given_AnError_Then_PrintsItOnTheJobsStderr()
        {
            var console = new BufferedConsole();
            var sink = new MemoryJobSink(console);

            await sink.ReportAsync(new JobMessage(JobMessageKind.Error, @"Error", string.Empty, @"The chart could not be saved"));

            console.Error.ShouldBe(@"Error: The chart could not be saved" + NewLineHelper.NewLine);
            console.Transcript.ShouldHaveSingleItem().ShouldBe(
                new JobTranscriptEntry { Kind = JobTranscriptKind.ErrorLine, Text = @"Error: The chart could not be saved" });
        }
    }
}
