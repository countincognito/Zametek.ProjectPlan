using Shouldly;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the waits of zpp --server when --server-timeout gives it no time limit: the request for the limits of the
    /// server has a time limit of its own, and the job has the time limit that the limits give. They call the client and not
    /// Main, and so they run beside the other tests: one of them waits 30 seconds.
    /// </summary>
    public class JobClientLimitsTests
    {
        // The answer to a job that printed and produced nothing, and succeeded.
        private const string c_NothingToDoAnswer = @"{""metrics"":{},""outputs"":[],""console"":{""exitCode"":0,""standardOutput"":"""",""standardError"":"""",""transcript"":[]}}";

        // How long a server that does not answer holds a request.
        private static readonly TimeSpan s_Hang = TimeSpan.FromMinutes(2);

        private static string Plan => Path.Combine(AppContext.BaseDirectory, @"Assets", @"two-scenarios.zpp");

        private static (Options Options, ClientSettings Settings) Run(string address)
        {
            var options = new Options { InputFilename = Plan, Server = address };
            return (options, ClientSettingsHelper.Resolve(options, new Dictionary<string, string>()).ShouldNotBeNull());
        }

        // A server that takes the connection and never answers, not even about its limits. The job would get no answer
        // either, and so the run ends.
        [Fact]
        public async Task Run_Given_AServerThatDoesNotAnswerAboutItsLimits_Then_ServerFailureAfterTheTimeForTheLimits()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            string address = $@"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
            using var stopped = new CancellationTokenSource();
            Task silent = Task.Run(async () =>
            {
                try
                {
                    using TcpClient connection = await listener.AcceptTcpClientAsync(stopped.Token);
                    await Task.Delay(s_Hang, stopped.Token);
                }
                catch (OperationCanceledException)
                {
                    // The test is over.
                }
            });
            (Options options, ClientSettings settings) = Run(address);

            // A client that does not give up is stopped, and the test fails.
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var waited = Stopwatch.StartNew();

            try
            {
                (await Should.ThrowAsync<ServerException>(() => JobClient.RunAsync(
                    options,
                    settings,
                    new RecordingJobConsole(),
                    limitsTimeout: TimeSpan.FromMilliseconds(500),
                    cancellationToken: guard.Token)))
                    .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServerLimitsTimedOut, address, 500, @"--server-timeout"));
                waited.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(400));
                waited.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
            }
            finally
            {
                await stopped.CancelAsync();
                await silent;
            }
        }

        // A server that gives its limits, takes the job, and never answers it.
        [Fact]
        public async Task Run_Given_NoServerTimeoutAndAJobThatGetsNoAnswer_Then_ServerFailureAtTheTimeThatTheLimitsGive()
        {
            // One job at a time, none in the queue, and no time for a job or a compilation: the time limit is the margin
            // alone, 30 seconds.
            await using FakeServer server = await FakeServer.StartAsync(
                _ => new FakeServer.Answer(Body: c_NothingToDoAnswer, Delay: s_Hang),
                () => FakeServer.InfoOf(1, 0, TimeSpan.Zero, TimeSpan.Zero));
            (Options options, ClientSettings settings) = Run(server.Address);

            // A client that does not give up is stopped, and the test fails.
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var waited = Stopwatch.StartNew();

            (await Should.ThrowAsync<ServerException>(() => JobClient.RunAsync(
                options,
                settings,
                new RecordingJobConsole(),
                cancellationToken: guard.Token)))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServerTimedOutFromLimits, server.Address, 30_000, @"--server-timeout"));
            waited.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(29));
            waited.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(55));
            server.Paths.ShouldBe([@"/v1/info", @"/v1/projects/compile"]);
        }
    }
}
