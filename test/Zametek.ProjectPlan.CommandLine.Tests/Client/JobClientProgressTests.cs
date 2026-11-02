using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// zpp --server asks the server how far its job has got, and says it in the log (it shows with --verbose, as the log of a run
    /// here does). The log is zpp's static one, and so these tests run with the other tests that run Main.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class JobClientProgressTests
        : IClassFixture<EngineFixture>, IDisposable
    {
        private const string ProgressPrefix = @"Simulation on ";

        private readonly EngineFixture m_Engine;
        private readonly string m_TempDirectory;
        private readonly ILogger m_PreviousLogger;

        public JobClientProgressTests(EngineFixture engine)
        {
            m_Engine = engine;
            m_TempDirectory = Path.Combine(Path.GetTempPath(), $@"zpp-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_TempDirectory);
            m_PreviousLogger = Log.Logger;
        }

        public void Dispose()
        {
            Log.Logger = m_PreviousLogger;
            Directory.Delete(m_TempDirectory, recursive: true);
        }

        private sealed class ListSink
            : ILogEventSink
        {
            private readonly List<string> m_Lines = [];
            private readonly TaskCompletionSource m_FirstProgress = new(TaskCreationOptions.RunContinuationsAsynchronously);

            // Completes when the log has said how far a job has got for the first time.
            public Task FirstProgress => m_FirstProgress.Task;

            public IReadOnlyList<string> Lines
            {
                get
                {
                    lock (m_Lines)
                    {
                        return [.. m_Lines];
                    }
                }
            }

            public void Emit(LogEvent logEvent)
            {
                string line = logEvent.RenderMessage();

                lock (m_Lines)
                {
                    m_Lines.Add(line);
                }

                if (line.StartsWith(ProgressPrefix, StringComparison.Ordinal))
                {
                    m_FirstProgress.TrySetResult();
                }
            }
        }

        // The sample plan, saved with a million iterations as the settings of its simulation: a run takes minutes.
        private async Task<string> LongSimulationAsync()
        {
            byte[] sample;
            await using (Stream stream = WarmUpService.OpenSample())
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory);
                sample = memory.ToArray();
            }

            using var input = new MemoryStream(sample);
            var sink = new MemoryJobSink(new BufferedConsole());
            await m_Engine.JobRunner.RunAsync(
                new JobRequest { Input = input, SaveProject = true, SimulationSettings = new SimulationSettingsModel { Iterations = 1_000_000 } },
                sink);

            string plan = Path.Combine(m_TempDirectory, @"long.zpp");
            await File.WriteAllBytesAsync(plan, sink.Outputs.Single(x => x.Output == JobOutput.Project).Content);
            return plan;
        }

        [Fact]
        public async Task Run_Given_AJobThatTakesLongerThanTheFirstQuestion_Then_TheLogSaysHowFarTheSimulationHasGot()
        {
            string plan = await LongSimulationAsync();
            var sink = new ListSink();
            Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Sink(sink).CreateLogger();

            // A job of a million iterations takes minutes. The test waits until the log has said how far it has got, however long that
            // takes on a machine that is busy, and then stops the run: how soon is the business of the machine and not of the test.
            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = new ServeLimits { MaxSimulationIterations = 1_000_000, JobTimeoutSeconds = 300 } });
            var options = new Options { InputFilename = plan, Server = server.Address, RunSimulation = true };
            ClientSettings settings = ClientSettingsHelper.Resolve(options, new Dictionary<string, string>()).ShouldNotBeNull();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

            Task<ExitCode> run = JobClient.RunAsync(options, settings, new RecordingJobConsole(), cancellationToken: cancellation.Token);
            await Task.WhenAny(sink.FirstProgress, run);

            sink.FirstProgress.IsCompleted.ShouldBeTrue("The run ended before the log said how far the job had got.");
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => run);

            string[] progress = [.. sink.Lines.Where(x => x.StartsWith(ProgressPrefix, StringComparison.Ordinal))];
            progress.ShouldNotBeEmpty();
            progress.ShouldAllBe(x => x.Contains(@"of 1000000 iterations", StringComparison.Ordinal));
            progress.ShouldAllBe(x => x.Contains(server.Address, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Run_Given_ALogThatDoesNotShowInformation_Then_NothingIsAskedAboutTheProgress()
        {
            string plan = await LongSimulationAsync();
            Log.Logger = new LoggerConfiguration().MinimumLevel.Warning().WriteTo.Sink(new ListSink()).CreateLogger();

            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = new ServeLimits { MaxSimulationIterations = 1_000_000, JobTimeoutSeconds = 4 } });
            var options = new Options { InputFilename = plan, Server = server.Address, RunSimulation = true };
            ClientSettings settings = ClientSettingsHelper.Resolve(options, new Dictionary<string, string>()).ShouldNotBeNull();

            await Should.ThrowAsync<ServerException>(() => JobClient.RunAsync(options, settings, new RecordingJobConsole()));

            // One request was the job, and none was a question: the log would not show the answer.
            server.ApiRequests.ShouldBe(1);
        }
    }
}
