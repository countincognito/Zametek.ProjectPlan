using Shouldly;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
            return RunningServer.JobContent(TwoScenarios(), @"two-scenarios.zpp");
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
        public async Task RunAsync_Given_OptionsItCanRunWith_Then_ServesUntilStopped()
        {
            // On a socket, which the test can find the server on without being told a port.
            string socket = NewSocketPath();
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);

            try
            {
                Task<ExitCode> running = JobServer.RunAsync([@"--unix-socket", socket], console, stopping.Token);

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
                ExitCode exitCode = await running;

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
        public async Task Request_Given_NoKey_Then_Unauthorized()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { ApiKey = c_ApiKey });

            using HttpResponseMessage info = await server.Client.GetAsync(@"/v1/info");
            using HttpResponseMessage job = await server.Client.PostAsync(@"/v1/jobs", Job());

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
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/jobs", held);
            await held.Started;
            await Task.Delay(s_Settle);

            using HttpResponseMessage turnedAway = await server.Client.PostAsync(@"/v1/jobs", Job());

            turnedAway.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            turnedAway.Headers.RetryAfter.ShouldNotBeNull().Delta.ShouldBe(TimeSpan.FromSeconds(5));
            (await ProblemDetailAsync(turnedAway)).ShouldBe(Resource.ProjectPlan.Messages.Message_ServeBusy);

            held.Release();
            using HttpResponseMessage ran = await running;
            using HttpResponseMessage next = await server.Client.PostAsync(@"/v1/jobs", Job());

            ran.StatusCode.ShouldBe(HttpStatusCode.OK);
            next.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RunJob_Given_AJobRunningAndOneWaiting_Then_TurnsTheNextAwayAndRunsTheOneWaiting()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxJobs = 1, MaxQueue = 1 } });
            using HeldContent held = await HeldContent.CreateAsync(Job());
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/jobs", held);
            await held.Started;
            await Task.Delay(s_Settle);
            Task<HttpResponseMessage> waiting = server.Client.PostAsync(@"/v1/jobs", Job());
            await Task.Delay(s_Settle);

            using HttpResponseMessage turnedAway = await server.Client.PostAsync(@"/v1/jobs", Job());
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
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/jobs", held, givenUp.Token);
            await held.Started;
            await Task.Delay(s_Settle);

            await givenUp.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(running);

            // The server notices the caller has gone as soon as it reads from the connection again.
            HttpStatusCode status = HttpStatusCode.ServiceUnavailable;
            for (int attempt = 0; attempt < 50 && status == HttpStatusCode.ServiceUnavailable; attempt++)
            {
                using HttpResponseMessage next = await server.Client.PostAsync(@"/v1/jobs", Job());
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
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/jobs", held);
            await held.Started;
            await Task.Delay(s_Settle);

            using var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/jobs") { Content = Job() };
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
            using var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/jobs")
            {
                Content = RunningServer.JobContent(new byte[2 * 1024 * 1024], @"large.zpp"),
            };
            request.Headers.ExpectContinue = true;
            using HttpResponseMessage response = await server.Client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
            (await ProblemDetailAsync(response)).ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeRequestTooLarge, 1));
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
                    new ServeSettings { UnixSocket = socket },
                    handler: UnixSocketHandler(socket));

                using HttpResponseMessage response = await server.Client.GetAsync(@"/v1/info");

                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                if (!OperatingSystem.IsWindows())
                {
                    // Only its owner may connect to it.
                    File.GetUnixFileMode(socket).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
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

        // A job whose upload stops part way until it is released. Its first bytes go at once, so that the server has
        // the request and has given it a job's place; the rest go when the test says.
        private sealed class HeldContent
            : HttpContent
        {
            private const int c_FirstBytes = 16;

            private readonly byte[] m_Body;
            private readonly TaskCompletionSource m_Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource m_Released = new(TaskCreationOptions.RunContinuationsAsynchronously);

            private HeldContent(byte[] body, MediaTypeHeaderValue? contentType)
            {
                m_Body = body;
                Headers.ContentType = contentType;
            }

            public Task Started => m_Started.Task;

            public static async Task<HeldContent> CreateAsync(MultipartFormDataContent job)
            {
                using (job)
                {
                    return new HeldContent(await job.ReadAsByteArrayAsync(), job.Headers.ContentType);
                }
            }

            public void Release()
            {
                m_Released.TrySetResult();
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            {
                return SerializeToStreamAsync(stream, context, CancellationToken.None);
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
            {
                await stream.WriteAsync(m_Body.AsMemory(0, c_FirstBytes), cancellationToken);
                await stream.FlushAsync(cancellationToken);
                m_Started.TrySetResult();

                await m_Released.Task.WaitAsync(cancellationToken);
                await stream.WriteAsync(m_Body.AsMemory(c_FirstBytes), cancellationToken);
            }

            protected override bool TryComputeLength(out long length)
            {
                // Sent in chunks, so that its first bytes go on their own.
                length = 0;
                return false;
            }
        }
    }
}
