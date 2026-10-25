using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// What Ctrl+C and SIGTERM do to a run of zpp: the first one stops the run after the step that is in progress, and the second
    /// is left to the system. A test sends the signals by calling the handler, since a test cannot send a signal to its own
    /// process without stopping the test run. ProgramCancellationTests has the runs that stop, and the verification of the real
    /// signals is on a Linux machine with kill.
    /// </summary>
    public class InterruptHandlerTests
    {
        [Fact]
        public void Handle_Given_TheFirstSignal_Then_SaysItWillStopCancelsTheRunAndGoesOn()
        {
            var console = new RecordingJobConsole();
            using var cancellation = new CancellationTokenSource();
            using var handler = new InterruptHandler(cancellation, console);

            handler.Handle().ShouldBeTrue();

            cancellation.IsCancellationRequested.ShouldBeTrue();
            console.Calls.ShouldBe([RecordingJobConsole.ErrorLine(Resource.ProjectPlan.Messages.Message_RunStopping)]);
            handler.Signals.ShouldBe(1);
        }

        [Fact]
        public void Handle_Given_TheSecondSignal_Then_LeavesTheProcessToTheSystemAndSaysNothingMore()
        {
            var console = new RecordingJobConsole();
            using var cancellation = new CancellationTokenSource();
            using var handler = new InterruptHandler(cancellation, console);
            handler.Handle();

            handler.Handle().ShouldBeFalse();
            handler.Handle().ShouldBeFalse();

            console.Calls.Count.ShouldBe(1);
            handler.Signals.ShouldBe(3);
        }

        [Fact]
        public void Handle_Given_ARunThatHasEnded_Then_DoesNotThrow()
        {
            var cancellation = new CancellationTokenSource();
            using var handler = new InterruptHandler(cancellation, new RecordingJobConsole());
            cancellation.Dispose();

            Should.NotThrow(() => handler.Handle().ShouldBeTrue());
        }

        [Fact]
        public void Handle_Given_AConsoleThatCannotBeWrittenTo_Then_StillCancelsTheRun()
        {
            using var cancellation = new CancellationTokenSource();
            using var handler = new InterruptHandler(cancellation, new BrokenConsole());

            Should.NotThrow(() => handler.Handle().ShouldBeTrue());

            cancellation.IsCancellationRequested.ShouldBeTrue();
        }

        [Fact]
        public void Dispose_Given_Registrations_Then_CanBeCalledMoreThanOnce()
        {
            using var cancellation = new CancellationTokenSource();
            var handler = new InterruptHandler(cancellation, new RecordingJobConsole());

            handler.Dispose();

            Should.NotThrow(handler.Dispose);
        }

        private sealed class BrokenConsole
            : IJobConsole
        {
            public Task WriteLineAsync(string text) => throw new IOException("stdout is closed");

            public Task DisplayAsync(string content, bool hasErrors) => throw new IOException("stdout is closed");

            public Task WriteErrorLineAsync(string text) => throw new IOException("stderr is closed");
        }
    }
}
