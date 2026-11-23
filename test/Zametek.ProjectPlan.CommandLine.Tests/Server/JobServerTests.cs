using Shouldly;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for what zpp serve does around its jobs: what it refuses to start
    /// with, the API key it asks for, the limits it works within - jobs at
    /// once, jobs waiting, the size of a request - when it says it is ready,
    /// and the transports it listens on besides TCP: a Unix domain socket, and
    /// TLS. In the same collection as ProgramExitCodeTests, because running the
    /// server sets the process's Serilog logger, as Main does.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class JobServerTests
        : IClassFixture<EngineFixture>
    {
        private const string c_ApiKey = @"s3cr3t-k3y";

        // How long a server started by RunAsync gets before it is stopped. A server told to start with what it should
        // refuse runs until it is stopped, so this is what stops it.
        private static readonly TimeSpan s_RunLimit = TimeSpan.FromSeconds(30);

        // How long a job held part way through its upload is given to reach the server, before another is sent: the
        // server takes the job's place when the request's headers arrive, which they do before its first bytes.
        private static readonly TimeSpan s_Settle = TimeSpan.FromMilliseconds(250);

        private readonly EngineFixture m_Engine;

        public JobServerTests(EngineFixture engine)
        {
            m_Engine = engine;
        }

        private static byte[] TwoScenarios()
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, @"Assets", @"two-scenarios.zpp"));
        }

        private static MultipartFormDataContent Job()
        {
            return RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
        }

        private Task<RunningServer> StartAsync(ServeSettings settings)
        {
            return RunningServer.StartAsync(m_Engine.JobRunner, settings);
        }

        private static async Task<string> ProblemDetailAsync(HttpResponseMessage response)
        {
            using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return problem.RootElement.GetProperty(@"detail").GetString() ?? string.Empty;
        }

        [Fact]
        public async Task RunAsync_Given_ALimitItCannotRunWith_Then_UsageErrorSayingWhy()
        {
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);

            ExitCode exitCode = await JobServer.RunAsync([@"--max-jobs", @"0"], console, stopping.Token);

            exitCode.ShouldBe(ExitCode.UsageError);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(
                string.Format(Resource.ProjectPlan.Messages.Message_ServeLimitTooLow, @"MaxJobs", @"--max-jobs", 1)));
        }

        [Fact]
        public async Task RunAsync_Given_AnAddressOtherMachinesCanReachWithoutAKey_Then_UsageErrorSayingWhy()
        {
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);

            ExitCode exitCode = await JobServer.RunAsync([@"--listen", @"http://0.0.0.0:0"], console, stopping.Token);

            exitCode.ShouldBe(ExitCode.UsageError);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServeNeedsApiKey,
                @"http://0.0.0.0:0",
                ServeSettingsHelper.ApiKeyVariable,
                @"--api-key-file")));
        }

        [Fact]
        public async Task RunAsync_Given_AUnixSocketWithoutAPath_Then_UsageErrorSayingWhy()
        {
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);

            ExitCode exitCode = await JobServer.RunAsync([@"--listen", @"unix:"], console, stopping.Token);

            exitCode.ShouldBe(ExitCode.UsageError);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServeUnixSocketNeedsPath,
                @"unix:")));
        }

        [Fact]
        public async Task RunAsync_Given_AUnixSocketWhosePathIsTooLong_Then_UsageErrorSayingSo()
        {
            // Rather than the system's own words for it, from the web server when it tries to listen there.
            string socket = Path.Combine(Path.GetTempPath(), new string('x', 300) + @".sock");
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);

            ExitCode exitCode = await JobServer.RunAsync([@"--listen", $@"unix:{socket}"], console, stopping.Token);

            exitCode.ShouldBe(ExitCode.UsageError);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(
                string.Format(Resource.ProjectPlan.Messages.Message_UnixSocketPathTooLong, socket)));
        }

        [Fact]
        public async Task RunAsync_Given_AnOptionWithoutItsValue_Then_UsageErrorSayingSo()
        {
            // The parser would drop --culture at the end, and the server would start in the machine's culture - on a port
            // of the system's choosing, so that a server that started after all would not take zpp serve's own.
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);

            ExitCode exitCode = await JobServer.RunAsync([@"--listen", @"http://127.0.0.1:0", @"--culture"], console, stopping.Token);

            exitCode.ShouldBe(ExitCode.UsageError);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(
                string.Format(Resource.ProjectPlan.Messages.Message_OptionNeedsValue, @"--culture")));
        }

        [Fact]
        public async Task RunAsync_Given_AWordForNothing_Then_UsageErrorNamingIt()
        {
            // As in zpp serve help, which the parser would take for zpp serve, and start the server - here on a port of the
            // system's choosing, as above.
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);

            ExitCode exitCode = await JobServer.RunAsync([@"help", @"--listen", @"http://127.0.0.1:0"], console, stopping.Token);

            exitCode.ShouldBe(ExitCode.UsageError);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ArgumentNotOptionOrValue,
                @"help",
                @"zpp serve --help")));
        }

        [Fact]
        public async Task RunAsync_Given_AnOptionGivenAgain_Then_UsageErrorSayingSo()
        {
            // The parser would keep the first size and ignore the second - here on a port of the system's choosing, as
            // above.
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);

            ExitCode exitCode = await JobServer.RunAsync(
                [@"--max-chart-size", @"600:400", @"--listen", @"http://127.0.0.1:0", @"--max-chart-size", @"700:500"],
                console,
                stopping.Token);

            exitCode.ShouldBe(ExitCode.UsageError);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(
                string.Format(Resource.ProjectPlan.Messages.Message_OptionGivenMoreThanOnce, @"--max-chart-size")));
        }

        [Fact]
        public async Task RunAsync_Given_OptionsItCanRunWith_Then_ServesUntilStopped()
        {
            // On a socket, which the test can find the server on without being told a port.
            string socket = NewSocketPath();

            try
            {
                (bool isLive, ExitCode exitCode, RecordingJobConsole console) = await ServeOnAsync(socket);

                isLive.ShouldBeTrue();
                exitCode.ShouldBe(ExitCode.Success);
                console.Calls.ShouldBeEmpty();
            }
            finally
            {
                File.Delete(socket);
            }
        }

        [Fact]
        public async Task RunAsync_Given_ASocketLeftBehindByAServerThatWasKilled_Then_RemovesItAndServes()
        {
            // A server that is started again, by whatever restarts it, after it was killed - which left its socket as it
            // was while it ran: the user's alone.
            string socket = NewSocketPath();

            try
            {
                SocketFiles.LeaveStale(socket, UnixSocketFileHelper.RestrictToOwner);

                (bool isLive, ExitCode exitCode, RecordingJobConsole console) = await ServeOnAsync(socket);

                isLive.ShouldBeTrue();
                exitCode.ShouldBe(ExitCode.Success);
                console.Calls.ShouldBeEmpty();
            }
            finally
            {
                File.Delete(socket);
            }
        }

        [Fact]
        public async Task RunAsync_Given_ASocketAServerListensOn_Then_FailureSayingSo()
        {
            // As it is for a port that is taken: the server cannot start, and the one that is there is left alone.
            string socket = NewSocketPath();
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);

            try
            {
                using Socket server = SocketFiles.ListenOn(socket);

                ExitCode exitCode = await JobServer.RunAsync([@"--listen", $@"unix:{socket}"], console, stopping.Token);

                exitCode.ShouldBe(ExitCode.Failure);
                console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(
                    string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketInUse, socket)));
                File.Exists(socket).ShouldBeTrue();
            }
            finally
            {
                File.Delete(socket);
            }
        }

        [Fact]
        public async Task RunAsync_Given_AFolderForTheSocketThatIsNotThere_Then_UsageErrorSayingSo()
        {
            string folder = Path.Combine(Path.GetTempPath(), $@"zpp-{Guid.NewGuid():N}"[..12]);
            string socket = Path.Combine(folder, @"zpp.sock");
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);

            ExitCode exitCode = await JobServer.RunAsync([@"--listen", $@"unix:{socket}"], console, stopping.Token);

            exitCode.ShouldBe(ExitCode.UsageError);
            console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(
                string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketFolderNotThere, folder, socket)));
            Directory.Exists(folder).ShouldBeFalse();
        }

        [Fact]
        public async Task RunAsync_Given_AFileWhereTheSocketIs_Then_UsageErrorSayingSoAndTheFileIsLeftAlone()
        {
            string file = Path.GetTempFileName();
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);

            try
            {
                File.WriteAllText(file, @"precious");

                ExitCode exitCode = await JobServer.RunAsync([@"--listen", $@"unix:{file}"], console, stopping.Token);

                exitCode.ShouldBe(ExitCode.UsageError);
                console.Calls.ShouldHaveSingleItem().ShouldBe(RecordingJobConsole.ErrorLine(
                    string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketIsAFile, file)));
                File.ReadAllText(file).ShouldBe(@"precious");
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public async Task Request_Given_NoKey_Then_Unauthorized()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { ApiKey = c_ApiKey });

            using HttpResponseMessage info = await server.Client.GetAsync(@"/v1/info");
            using HttpResponseMessage job = await server.Client.PostAsync(@"/v1/projects/compile", Job());

            info.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            info.Headers.WwwAuthenticate.ShouldHaveSingleItem().Scheme.ShouldBe(@"Bearer");
            (await ProblemDetailAsync(info)).ShouldBe(Resource.ProjectPlan.Messages.Message_ServeApiKeyRequired);
            job.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData(@"Bearer wrong-key")]
        [InlineData(@"Bearer s3cr3t-k3")]
        [InlineData(@"Basic s3cr3t-k3y")]
        [InlineData(@"s3cr3t-k3y")]
        public async Task Request_Given_AnyOtherKey_Then_Unauthorized(string authorization)
        {
            await using RunningServer server = await StartAsync(new ServeSettings { ApiKey = c_ApiKey });

            using var request = new HttpRequestMessage(HttpMethod.Get, @"/v1/info");
            request.Headers.TryAddWithoutValidation(@"Authorization", authorization);
            using HttpResponseMessage response = await server.Client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData(@"Bearer s3cr3t-k3y")]
        [InlineData(@"bearer s3cr3t-k3y")]
        [InlineData(@"BEARER  s3cr3t-k3y ")]
        public async Task Request_Given_TheKey_Then_Served(string authorization)
        {
            await using RunningServer server = await StartAsync(new ServeSettings { ApiKey = c_ApiKey });

            using var request = new HttpRequestMessage(HttpMethod.Get, @"/v1/info");
            request.Headers.TryAddWithoutValidation(@"Authorization", authorization);
            using HttpResponseMessage response = await server.Client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Health_Given_AServerWithAKey_Then_NeedsNoKey()
        {
            // So that a load balancer or an orchestrator can ask.
            await using RunningServer server = await StartAsync(new ServeSettings { ApiKey = c_ApiKey });

            using HttpResponseMessage live = await server.Client.GetAsync(@"/health/live");

            live.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RunJob_Given_AsManyJobsAsTheLimitsAllow_Then_TurnsTheNextAwayUntilOneFinishes()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxJobs = 1, MaxQueue = 0 } });
            using HeldContent held = await HeldContent.CreateAsync(Job());
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/projects/compile", held);
            await held.Started;
            await Task.Delay(s_Settle);

            using HttpResponseMessage turnedAway = await server.Client.PostAsync(@"/v1/projects/compile", Job());

            turnedAway.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            turnedAway.Headers.RetryAfter.ShouldNotBeNull().Delta.ShouldBe(TimeSpan.FromSeconds(5));
            (await ProblemDetailAsync(turnedAway)).ShouldBe(Resource.ProjectPlan.Messages.Message_ServeBusy);

            held.Release();
            using HttpResponseMessage ran = await running;
            using HttpResponseMessage next = await server.Client.PostAsync(@"/v1/projects/compile", Job());

            ran.StatusCode.ShouldBe(HttpStatusCode.OK);
            next.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RunJob_Given_AJobRunningAndOneWaiting_Then_TurnsTheNextAwayAndRunsTheOneWaiting()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxJobs = 1, MaxQueue = 1 } });
            using HeldContent held = await HeldContent.CreateAsync(Job());
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/projects/compile", held);
            await held.Started;
            await Task.Delay(s_Settle);
            Task<HttpResponseMessage> waiting = server.Client.PostAsync(@"/v1/projects/compile", Job());
            await Task.Delay(s_Settle);

            using HttpResponseMessage turnedAway = await server.Client.PostAsync(@"/v1/projects/compile", Job());
            waiting.IsCompleted.ShouldBeFalse();
            held.Release();

            turnedAway.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await running).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await waiting).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RunJob_Given_AJobItsCallerGivesUpOn_Then_ItsPlaceIsFreed()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxJobs = 1, MaxQueue = 0 } });
            using HeldContent held = await HeldContent.CreateAsync(Job());
            using var givenUp = new CancellationTokenSource();
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/projects/compile", held, givenUp.Token);
            await held.Started;
            await Task.Delay(s_Settle);

            await givenUp.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(running);

            // The server notices the caller has gone as soon as it reads from the connection again.
            HttpStatusCode status = HttpStatusCode.ServiceUnavailable;
            for (int attempt = 0; attempt < 50 && status == HttpStatusCode.ServiceUnavailable; attempt++)
            {
                using HttpResponseMessage next = await server.Client.PostAsync(@"/v1/projects/compile", Job());
                status = next.StatusCode;
                if (status == HttpStatusCode.ServiceUnavailable)
                {
                    await Task.Delay(100);
                }
            }

            status.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RunJob_Given_NoKeyWhileTheServerIsFull_Then_UnauthorizedRatherThanBusy()
        {
            // A request without the key never takes a job's place, or waits for one.
            await using RunningServer server = await StartAsync(new ServeSettings
            {
                ApiKey = c_ApiKey,
                Limits = new ServeLimits { MaxJobs = 1, MaxQueue = 0 },
            });
            server.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(@"Bearer", c_ApiKey);
            using HeldContent held = await HeldContent.CreateAsync(Job());
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/projects/compile", held);
            await held.Started;
            await Task.Delay(s_Settle);

            using var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/projects/compile") { Content = Job() };
            request.Headers.Authorization = new AuthenticationHeaderValue(@"Bearer", @"wrong-key");
            using HttpResponseMessage unauthorized = await server.Client.SendAsync(request);
            held.Release();

            unauthorized.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await running).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RunJob_Given_ARequestLargerThanTheLimit_Then_PayloadTooLarge()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxUploadMegabytes = 1 } });

            // Asking to continue first, so that the server answers before the request is sent in full.
            using var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/projects/compile")
            {
                Content = RunningServer.CompileContent(new byte[2 * 1024 * 1024], @"large.zpp"),
            };
            request.Headers.ExpectContinue = true;
            using HttpResponseMessage response = await server.Client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
            (await ProblemDetailAsync(response)).ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeRequestTooLarge, 1));
        }

        [Fact]
        public async Task RunJob_Given_ARequestInChunksLargerThanTheLimit_Then_PayloadTooLarge()
        {
            // Each other test of the answer 413 sends a body with Content-Length. A body in chunks has no length, and the web
            // server finds that it is too large only while it reads it (from the review of src, test and pkg).
            //
            // The test writes the request on a socket, and it stops a little after the limit. Then it reads the answer. Before,
            // HttpClient sent all of the body, and the server answered and closed the connection while the client still sent.
            // On a busy computer the client then got a reset of the connection and no answer (the hook of 2026-11-23, with
            // other tests in WSL at the same time). HttpClient reads the answer only after it has sent the body.
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxUploadMegabytes = 1 } });
            using MultipartFormDataContent form = RunningServer.CompileContent(new byte[2 * 1024 * 1024], @"large.zpp");
            byte[] body = await form.ReadAsByteArrayAsync();
            var address = new Uri(server.Address);

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(address.Host, address.Port);
            await using NetworkStream network = tcp.GetStream();
            string head = $"POST /v1/projects/compile HTTP/1.1\r\nHost: {address.Authority}\r\nContent-Type: {form.Headers.ContentType}\r\nTransfer-Encoding: chunked\r\n\r\n";
            await network.WriteAsync(Encoding.ASCII.GetBytes(head));

            const int chunk = 64 * 1024;

            for (int sent = 0; sent < (1024 * 1024) + chunk; sent += chunk)
            {
                await network.WriteAsync(Encoding.ASCII.GetBytes($"{chunk:X}\r\n"));
                await network.WriteAsync(body.AsMemory(sent, chunk));
                await network.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            }

            await network.FlushAsync();

            // The server closes the connection after the answer.
            using var reader = new StreamReader(network, Encoding.UTF8);
            string answer = await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromMinutes(1));

            answer.ShouldStartWith(@"HTTP/1.1 413 ");
            answer.ShouldContain(JsonSerializer.Serialize(string.Format(Resource.ProjectPlan.Messages.Message_ServeRequestTooLarge, 1), ProjectEndpoints.JsonOptions));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        public async Task Build_Given_ALimitOfTheConnections_Then_TheWebServerHasIt(int connections)
        {
            // Kestrel has no limit of the connections by default, and the paths that need no key are outside the limiter of
            // the jobs (from the review of src, test and pkg).
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxConnections = connections } });

            Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions kestrel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>>(server.Services)
                .Value;

            kestrel.Limits.MaxConcurrentConnections.ShouldBe(connections);
        }

        [Fact]
        public async Task Request_Given_AConnectionAboveTheLimit_Then_ClosedWithNoAnswer()
        {
            // The client of the harness keeps its connection open after its first request, and so it holds the only place. A
            // second client then gets its connection closed, and no answer. The first client still gets answers.
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxConnections = 1 } });
            using (HttpResponseMessage first = await server.Client.GetAsync(@"/health/live"))
            {
                first.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            using var other = new HttpClient { BaseAddress = server.Client.BaseAddress, Timeout = TimeSpan.FromMinutes(1) };
            await Should.ThrowAsync<HttpRequestException>(() => other.GetAsync(@"/health/live"));

            using HttpResponseMessage again = await server.Client.GetAsync(@"/health/live");
            again.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(50)]
        public async Task Build_Given_ALimitOfTheUpload_Then_APartUpToThatSizeStaysInMemory(int megabytes)
        {
            // With the default threshold of 64 KB, the framework kept a larger part in a temporary file, and the plan of each
            // such job went through the disk (from the review of src, test and pkg).
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxUploadMegabytes = megabytes } });

            Microsoft.AspNetCore.Http.Features.FormOptions form = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Features.FormOptions>>(server.Services)
                .Value;

            form.MemoryBufferThreshold.ShouldBe(megabytes * 1024 * 1024);
            form.MultipartBodyLengthLimit.ShouldBe(megabytes * 1024L * 1024L);
        }

        [Fact]
        public async Task Health_Given_AServerThatWarmsUp_Then_ReadyOnceItHas()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings(), warmUp: true);

            HttpStatusCode status = HttpStatusCode.ServiceUnavailable;
            for (int attempt = 0; attempt < 600 && status != HttpStatusCode.OK; attempt++)
            {
                using HttpResponseMessage ready = await server.Client.GetAsync(@"/health/ready");
                status = ready.StatusCode;
                if (status != HttpStatusCode.OK)
                {
                    await Task.Delay(100);
                }
            }

            status.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Request_Given_AUnixDomainSocket_Then_ServedThere()
        {
            string socket = NewSocketPath();

            try
            {
                await using RunningServer server = await RunningServer.StartAsync(
                    m_Engine.JobRunner,
                    new ServeSettings { UnixSockets = [socket] },
                    handler: UnixSocketHandler(socket));

                using HttpResponseMessage response = await server.Client.GetAsync(@"/v1/info");

                // Only its owner may connect to it - as it is, who has just done so.
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                SocketAccess.AssertOwnerOnly(socket);
            }
            finally
            {
                File.Delete(socket);
            }
        }

        [Fact]
        public async Task Request_Given_TwoUnixDomainSockets_Then_ServedOnEach()
        {
            string first = NewSocketPath();
            string second = NewSocketPath();

            try
            {
                await using RunningServer server = await RunningServer.StartAsync(
                    m_Engine.JobRunner,
                    new ServeSettings { UnixSockets = [first, second] },
                    handler: UnixSocketHandler(first));
                using var other = new HttpClient(UnixSocketHandler(second)) { BaseAddress = new Uri(@"http://localhost") };

                using HttpResponseMessage onFirst = await server.Client.GetAsync(@"/v1/info");
                using HttpResponseMessage onSecond = await other.GetAsync(@"/v1/info");

                onFirst.StatusCode.ShouldBe(HttpStatusCode.OK);
                onSecond.StatusCode.ShouldBe(HttpStatusCode.OK);
                SocketAccess.AssertOwnerOnly(first);
                SocketAccess.AssertOwnerOnly(second);
            }
            finally
            {
                File.Delete(first);
                File.Delete(second);
            }
        }

        [Fact]
        public async Task Request_Given_AUnixDomainSocketAndAnAddress_Then_ServedOnBoth()
        {
            string socket = NewSocketPath();

            try
            {
                await using RunningServer server = await StartAsync(new ServeSettings
                {
                    Listen = [ServeSettingsHelper.ParseListenAddress(@"http://127.0.0.1:0")],
                    UnixSockets = [socket],
                });
                using var onSocket = new HttpClient(UnixSocketHandler(socket)) { BaseAddress = new Uri(@"http://localhost") };

                using HttpResponseMessage overTcp = await server.Client.GetAsync(@"/v1/info");
                using HttpResponseMessage overSocket = await onSocket.GetAsync(@"/v1/info");

                overTcp.StatusCode.ShouldBe(HttpStatusCode.OK);
                overSocket.StatusCode.ShouldBe(HttpStatusCode.OK);
            }
            finally
            {
                File.Delete(socket);
            }
        }

        [Theory]
        [InlineData(@"pfx")]
        [InlineData(@"pem with key")]
        [InlineData(@"pem and key")]
        public async Task Request_Given_Https_Then_ServedWithTheCertificate(string form)
        {
            string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-tls-{Guid.NewGuid():N}")).FullName;

            try
            {
                using X509Certificate2 certificate = LoadCertificate(form, directory);
                var handler = new SocketsHttpHandler();
                handler.SslOptions.RemoteCertificateValidationCallback = (_, presented, _, _) =>
                    presented is not null && presented.GetCertHashString() == certificate.GetCertHashString();

                await using RunningServer server = await RunningServer.StartAsync(
                    m_Engine.JobRunner,
                    new ServeSettings
                    {
                        Listen = [ServeSettingsHelper.ParseListenAddress(@"https://127.0.0.1:0")],
                        Certificate = certificate,
                    },
                    handler: handler);

                server.Address.ShouldStartWith(@"https://");
                using HttpResponseMessage response = await server.Client.GetAsync(@"/v1/info");

                response.StatusCode.ShouldBe(HttpStatusCode.OK);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        // A path for a Unix domain socket, short enough for every platform's limit on one.
        private static string NewSocketPath()
        {
            return Path.Combine(Path.GetTempPath(), $@"zpp-{Guid.NewGuid():N}"[..12] + @".sock");
        }

        // Runs zpp serve on the socket until it answers its health check, and then stops it: whether it did answer, how it
        // ended, and what it said to the console. A server that ends first, or does not answer in time, did not answer.
        private static async Task<(bool IsLive, ExitCode ExitCode, RecordingJobConsole Console)> ServeOnAsync(string socket)
        {
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);

            Task<ExitCode> running = JobServer.RunAsync([@"--listen", $@"unix:{socket}"], console, stopping.Token);

            using var client = new HttpClient(UnixSocketHandler(socket)) { BaseAddress = new Uri(@"http://localhost") };
            bool isLive = false;
            for (int attempt = 0; attempt < 300 && !isLive && !running.IsCompleted; attempt++)
            {
                try
                {
                    using HttpResponseMessage live = await client.GetAsync(@"/health/live");
                    isLive = live.StatusCode == HttpStatusCode.OK;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(100);
                }
            }

            await stopping.CancelAsync();
            return (isLive, await running, console);
        }

        // A handler that connects to the server on its socket, whatever the request's address.
        private static SocketsHttpHandler UnixSocketHandler(string socket)
        {
            return new SocketsHttpHandler
            {
                ConnectCallback = async (_, cancellationToken) =>
                {
                    var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await client.ConnectAsync(new UnixDomainSocketEndPoint(socket), cancellationToken);
                    return new NetworkStream(client, ownsSocket: true);
                },
            };
        }

        // A self-signed certificate for this machine, written as the form says, and read back as zpp serve reads it.
        private static X509Certificate2 LoadCertificate(
            string form,
            string directory)
        {
            using RSA key = RSA.Create(2048);
            var request = new CertificateRequest(@"CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddIpAddress(IPAddress.Loopback);
            names.AddDnsName(@"localhost");
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(@"1.3.6.1.5.5.7.3.1")], critical: false));
            using X509Certificate2 created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

            string certificateFile = Path.Combine(directory, form == @"pfx" ? @"server.pfx" : @"server.pem");
            string keyFile = Path.Combine(directory, @"server.key");

            switch (form)
            {
                case @"pfx":
                    File.WriteAllBytes(certificateFile, created.Export(X509ContentType.Pkcs12, @"p4ssw0rd"));
                    return ServeSettingsHelper.LoadCertificate(certificateFile, null, @"p4ssw0rd");
                case @"pem with key":
                    File.WriteAllText(certificateFile, created.ExportCertificatePem() + Environment.NewLine + key.ExportPkcs8PrivateKeyPem());
                    return ServeSettingsHelper.LoadCertificate(certificateFile, null, null);
                default:
                    File.WriteAllText(certificateFile, created.ExportCertificatePem());
                    File.WriteAllText(keyFile, key.ExportPkcs8PrivateKeyPem());
                    return ServeSettingsHelper.LoadCertificate(certificateFile, keyFile, null);
            }
        }
    }
}
