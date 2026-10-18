using Shouldly;
using System.Diagnostics;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// A run of zpp that is stopped, as Ctrl+C stops it: it ends with the code of a stop, says so, and writes nothing more. The
    /// tests cancel the token that Main takes, which is the token that the first signal cancels (InterruptHandlerTests). They
    /// are in the collection of the other tests that run Main, since Main swaps the streams of the console while it runs.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class ProgramCancellationTests
        : IClassFixture<EngineFixture>, IDisposable
    {
        private readonly EngineFixture m_Engine;
        private readonly string m_TempDirectory;

        public ProgramCancellationTests(EngineFixture engine)
        {
            m_Engine = engine;
            m_TempDirectory = Path.Combine(Path.GetTempPath(), $@"zpp-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_TempDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(m_TempDirectory, recursive: true);
        }

        private static string Estimated => Path.Combine(AppContext.BaseDirectory, @"Assets", @"estimated.zpp");

        // The sample plan, saved with a million iterations as the settings of its simulation: a run takes minutes, and the test
        // stops it long before that.
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
        public async Task Main_Given_ATokenThatIsCancelledBeforeTheRun_Then_ExitsWithTheCodeOfAStopAndSaysSo()
        {
            string output = Path.Combine(m_TempDirectory, @"out.zpp");
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            (int exitCode, string standardOutput, string standardError) = await ZppMain.RunCapturedAsync(
                [@"-i", Estimated, @"-o", output],
                cancellationToken: cancellation.Token);

            exitCode.ShouldBe((int)ExitCode.Cancelled);
            standardError.ShouldContain(Resource.ProjectPlan.Messages.Message_RunCancelled);

            // A run that stopped has not got to the metrics, and has not saved the project.
            standardOutput.ShouldBeEmpty();
            File.Exists(output).ShouldBeFalse();
        }

        [Fact]
        public async Task Main_Given_ATokenThatIsCancelledWhileASimulationRuns_Then_StopsEarlyWritesNothingAndExitsWithTheCodeOfAStop()
        {
            string plan = await LongSimulationAsync();
            string output = Path.Combine(m_TempDirectory, @"out.zpp");
            using var cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromSeconds(3));

            // A million iterations of the sample plan take minutes. The run ends in the time that a chunk of the iterations takes
            // after the cancel, and not when the simulation ends: how soon depends on the machine, and so the test allows a minute.
            var stopwatch = Stopwatch.StartNew();
            (int exitCode, string standardOutput, string standardError) = await ZppMain.RunCapturedAsync(
                [@"-i", plan, @"--run-simulation", @"-o", output],
                cancellationToken: cancellation.Token);
            stopwatch.Stop();

            exitCode.ShouldBe((int)ExitCode.Cancelled);
            standardError.ShouldContain(Resource.ProjectPlan.Messages.Message_RunCancelled);
            standardOutput.ShouldBeEmpty();
            File.Exists(output).ShouldBeFalse();
            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task Main_Given_ATokenThatIsCancelledWhileTheRunWaitsForABusyServer_Then_StopsAtOnceWithTheCodeOfAStop()
        {
            // A server that is busy and says to come back in a minute: zpp waits, and the stop ends the wait.
            await using FakeServer server = await FakeServer.StartAsync(_ => new FakeServer.Answer(
                503,
                Headers: new Dictionary<string, string> { [@"Retry-After"] = @"60" }));
            using var cancellation = new CancellationTokenSource();

            Task<(int ExitCode, string Output, string Error)> run = ZppMain.RunCapturedAsync(
                [@"-i", Estimated, @"--server", server.Address],
                cancellationToken: cancellation.Token);

            // Cancel once the server has had the first request, which is the time that zpp starts to wait.
            var waited = Stopwatch.StartNew();
            while (server.Requests.Count == 0 && waited.Elapsed < TimeSpan.FromSeconds(30))
            {
                await Task.Delay(20);
            }

            server.Requests.Count.ShouldBe(1);
            await cancellation.CancelAsync();
            (int exitCode, _, string standardError) = await run;

            exitCode.ShouldBe((int)ExitCode.Cancelled);
            standardError.ShouldContain(Resource.ProjectPlan.Messages.Message_RunCancelled);
            waited.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(45));
        }

        [Fact]
        public async Task Main_Given_ATokenThatIsNeverCancelled_Then_RunsAsItDidBefore()
        {
            using var cancellation = new CancellationTokenSource();

            (int exitCode, string standardOutput, _) = await ZppMain.RunCapturedAsync([@"-i", Estimated], cancellationToken: cancellation.Token);

            exitCode.ShouldBe((int)ExitCode.Success);
            standardOutput.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task Main_Given_ATokenThatIsCancelledForAListOfScenarios_Then_ExitsWithTheCodeOfAStop()
        {
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            (int exitCode, string standardOutput, _) = await ZppMain.RunCapturedAsync(
                [@"-i", Estimated, @"--list-scenarios"],
                cancellationToken: cancellation.Token);

            exitCode.ShouldBe((int)ExitCode.Cancelled);
            standardOutput.ShouldBeEmpty();
        }
    }
}
