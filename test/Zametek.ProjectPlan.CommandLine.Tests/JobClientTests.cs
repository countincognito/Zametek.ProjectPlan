using Microsoft.AspNetCore.Http;
using Shouldly;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp --server around the job: where it runs - on --server, on
    /// ZPP_SERVER unless --local says otherwise, or here - and what it refuses
    /// before anything is sent; how it talks to the server - the request it sends
    /// and the problems it tells apart by their types; a server that does not
    /// run the job - one that is not there or not trusted, wants its API key, is
    /// asked for more than its limits allow, stays busy for longer than zpp may
    /// wait, runs out of time, or answers with something zpp cannot read - which
    /// ends the run with exit code 5, saying why, and with the request's id; one
    /// that is busy for a while, which zpp tries again when it says to; and a
    /// job that ran and failed, which ends the run as it would have ended here.
    /// What a job a server runs prints and writes - as zpp would -
    /// JobClientParityTests pins. In the same collection as ProgramExitCodeTests,
    /// because Main swaps the console's streams while it runs.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class JobClientTests
        : IClassFixture<EngineFixture>, IDisposable
    {
        private const string c_ApiKey = @"s3cr3t-k3y";

        // The answer to a job that printed and produced nothing, and succeeded.
        private const string c_NothingToDoAnswer = @"{""metrics"":{},""outputs"":[],""console"":{""exitCode"":0,""standardOutput"":"""",""standardError"":"""",""transcript"":[]}}";

        // How long a job held part way through its upload is given to reach the server, before another is sent.
        private static readonly TimeSpan s_Settle = TimeSpan.FromMilliseconds(250);

        // How long a client that should have given up is given to, before it is stopped.
        private static readonly TimeSpan s_Hang = TimeSpan.FromSeconds(30);

        // A line a log wrote on stderr.
        private static readonly Regex s_LogLine = new(@"^\[\d\d:\d\d:\d\d [A-Z]{3}\] .*(\r?\n|$)", RegexOptions.Multiline);

        // The id of the request that an error says.
        private static readonly Regex s_TraceId = new(@"\(trace id ([0-9a-f]{32})\)");

        private readonly EngineFixture m_Engine;
        private readonly string m_TempDirectory;

        public JobClientTests(EngineFixture engine)
        {
            m_Engine = engine;
            m_TempDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-client-{Guid.NewGuid():N}")).FullName;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(m_TempDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }

            GC.SuppressFinalize(this);
        }

        private static string Plan => Path.Combine(AppContext.BaseDirectory, @"Assets", @"two-scenarios.zpp");

        private static Dictionary<string, string> Variables(params (string Name, string Value)[] variables)
        {
            return variables.ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }

        // zpp's Main, with what it printed - less the log, which a server running in this process writes on the same
        // stderr as zpp.
        private static async Task<(int ExitCode, string Output, string Error)> RunAsync(
            string[] args,
            IReadOnlyDictionary<string, string>? environment = null)
        {
            (int exitCode, string output, string error) = await ZppMain.RunCapturedAsync(args, environment);
            return (exitCode, output, s_LogLine.Replace(error, string.Empty));
        }

        private static string ErrorLine(string message)
        {
            return message + System.Environment.NewLine;
        }

        private Task<RunningServer> StartAsync(ServeSettings? settings = null)
        {
            return RunningServer.StartAsync(m_Engine.JobRunner, settings);
        }

        // An address on this machine that nothing listens on.
        private static string AddressNothingListensOn()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return $@"http://127.0.0.1:{port}";
        }

        // A certificate for this machine that the server made for itself, which nothing on the machine vouches for - read
        // as zpp serve reads one.
        private X509Certificate2 SelfSignedCertificate()
        {
            using RSA key = RSA.Create(2048);
            var request = new CertificateRequest(@"CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
            using X509Certificate2 created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

            string file = Path.Combine(m_TempDirectory, @"server.pfx");
            File.WriteAllBytes(file, created.Export(X509ContentType.Pkcs12, @"p4ssw0rd"));
            return ServeSettingsHelper.LoadCertificate(file, null, @"p4ssw0rd");
        }

        private static string Json<T>(T value)
        {
            return JsonSerializer.Serialize(value, JobJsonHelper.ServerOptions);
        }

        // The console of a job that ended with the exit code, and printed the line - if it did - on stderr.
        private static ConsoleResponse ConsoleOf(
            ExitCode exitCode,
            string? errorLine = null)
        {
            return new ConsoleResponse(
                (int)exitCode,
                string.Empty,
                errorLine is null ? string.Empty : errorLine + "\n",
                errorLine is null ? [] : [new JobTranscriptEntry { Kind = JobTranscriptKind.ErrorLine, Text = errorLine }]);
        }

        // A problem of the kind, as a server answers with it.
        private static FakeServer.Answer ProblemOf(
            ProblemKind kind,
            ConsoleResponse? console = null,
            string? detail = null,
            IReadOnlyList<ProblemError>? errors = null,
            IReadOnlyList<OutputResponse>? outputs = null,
            IReadOnlyDictionary<string, string>? headers = null)
        {
            var problem = new ProblemResponse
            {
                Type = ProblemHelper.GetType(kind),
                Title = ProblemHelper.GetTitle(kind),
                Status = ProblemHelper.GetStatus(kind),
                Detail = detail,
                TraceId = new string('a', 32),
                Errors = errors,
                Metrics = outputs is null ? null : new MetricsResponse(),
                Outputs = outputs,
                Console = console,
            };

            return new FakeServer.Answer(problem.Status, Json(problem), ProblemHelper.MediaType, headers);
        }

        private static FakeServer.Answer Busy(string retryAfter)
        {
            return ProblemOf(ProblemKind.Busy, detail: Resource.ProjectPlan.Messages.Message_ServeBusy, headers: new Dictionary<string, string> { [@"Retry-After"] = retryAfter });
        }

        private static FakeServer.Answer Ok(string body = c_NothingToDoAnswer)
        {
            return new FakeServer.Answer(Body: body);
        }

        // The reason zpp ends a run with when a server did not run the job, in the words zpp says it in, with the id of the
        // request - which zpp made, and sent in its traceparent, and which the server's log names the request by.
        private static void ShouldBeRefused(
            string error,
            string address,
            string reason,
            string? traceId = null)
        {
            Match match = s_TraceId.Match(error);
            match.Success.ShouldBeTrue(error);

            if (traceId is not null)
            {
                match.Groups[1].Value.ShouldBe(traceId);
            }

            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerRefused,
                address,
                string.Format(Resource.ProjectPlan.Messages.Message_ServerReasonWithRequestId, reason, match.Groups[1].Value))));
        }

        // The id of the request, as the traceparent it came with says it.
        private static string TraceIdOf(FakeServer.Request request)
        {
            return request.Headers[@"traceparent"].Split('-')[1];
        }

        [Fact]
        public async Task Run_Given_AServerThatIsNotThere_Then_ServerFailureSayingSo()
        {
            string address = AddressNothingListensOn();

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server", address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            error.ShouldStartWith(string.Format(Resource.ProjectPlan.Messages.Message_ServerUnreachable, address, string.Empty));
        }

        [Fact]
        public async Task Run_Given_ASocketThatIsNotThere_Then_ServerFailureSayingSo()
        {
            string socket = Path.Combine(m_TempDirectory, @"zpp.sock");

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server", $@"unix:{socket}"]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerUnreachable,
                $@"unix:{socket}",
                string.Format(Resource.ProjectPlan.Messages.Message_ServerSocketNotThere, socket))));
        }

        [Fact]
        public async Task Run_Given_AServerWhoseCertificateThisMachineDoesNotTrust_Then_ServerFailureSayingWhyWithoutSendingTheJob()
        {
            using X509Certificate2 certificate = SelfSignedCertificate();
            await using RunningServer server = await StartAsync(new ServeSettings
            {
                Listen = [ServeSettingsHelper.ParseListenAddress(@"https://127.0.0.1:0")],
                Certificate = certificate,
            });

            // Why this machine will not connect to it, in the words of the error at the root of it.
            using var client = new HttpClient();
            HttpRequestException refused = await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync(server.Address));

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            error.ShouldBe(ErrorLine(string.Format(Resource.ProjectPlan.Messages.Message_ServerUnreachable, server.Address, refused.GetBaseException().Message)));
            server.ApiRequests.ShouldBe(0);
        }

        [Fact]
        public async Task Run_Given_AServerThatNeedsAKeyAndNone_Then_ServerFailureSayingWhereItGoes()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { ApiKey = c_ApiKey });

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerNeedsApiKey,
                server.Address,
                ClientSettingsHelper.ApiKeyVariable,
                @"--api-key-file")));
        }

        [Fact]
        public async Task Run_Given_AServerThatNeedsAKeyAndTheWrongOne_Then_ServerFailureSayingSo()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { ApiKey = c_ApiKey });

            (int exitCode, _, string error) = await RunAsync(
                [@"-i", Plan, @"--server", server.Address],
                Variables((ClientSettingsHelper.ApiKeyVariable, @"not-the-key")));

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            error.ShouldBe(ErrorLine(string.Format(Resource.ProjectPlan.Messages.Message_ServerRefusedApiKey, server.Address)));
        }

        [Fact]
        public async Task Run_Given_TheKeyInTheEnvironment_Then_RunsTheJob()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { ApiKey = c_ApiKey });

            (int exitCode, _, _) = await RunAsync(
                [@"-i", Plan, @"--server", server.Address],
                Variables((ClientSettingsHelper.ApiKeyVariable, c_ApiKey)));

            exitCode.ShouldBe((int)ExitCode.Success);
        }

        [Fact]
        public async Task Run_Given_TheKeyInAFile_Then_RunsTheJob()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { ApiKey = c_ApiKey });
            string keyFile = Path.Combine(m_TempDirectory, @"api-key");
            await File.WriteAllTextAsync(keyFile, c_ApiKey + System.Environment.NewLine);

            (int exitCode, _, _) = await RunAsync([@"-i", Plan, @"--server", server.Address, @"--api-key-file", keyFile]);

            exitCode.ShouldBe((int)ExitCode.Success);
        }

        [Fact]
        public async Task Run_Given_AChartBeyondTheServersLimits_Then_ServerFailureWithTheServersReasonAndWhereItIs()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxChartWidth = 300 } });

            (int exitCode, _, string error) = await RunAsync(
                [@"-i", Plan, @"--server", server.Address, @"--gantt-directory", m_TempDirectory, @"--gantt-size", @"800:600"]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            ShouldBeRefused(
                error,
                server.Address,
                $@"{Resource.ProjectPlan.Messages.Message_ServeRequestHasAProblem.TrimEnd('.')}: #/options/outputs/ganttChart/width {string.Format(Resource.ProjectPlan.Messages.Message_ServeErrorPixelsOutOfRange, 300)}");
            Directory.GetFiles(m_TempDirectory).ShouldBeEmpty();
        }

        [Fact]
        public async Task Run_Given_APlanLargerThanTheServerTakes_Then_ServerFailureWithTheServersReason()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxUploadMegabytes = 1 } });
            string plan = Path.Combine(m_TempDirectory, @"large.zpp");
            await File.WriteAllBytesAsync(plan, new byte[2 * 1024 * 1024]);

            (int exitCode, _, string error) = await RunAsync([@"-i", plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            ShouldBeRefused(error, server.Address, string.Format(Resource.ProjectPlan.Messages.Message_ServeRequestTooLarge, 1));
        }

        [Fact]
        public async Task Run_Given_AJobThatRunsOutOfTimeOnTheServer_Then_ServerFailureWithTheServersReasonAndNoSecondTry()
        {
            // A limit of no time at all is one no job can keep to.
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { JobTimeoutSeconds = 0 } });

            (int exitCode, _, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            ShouldBeRefused(error, server.Address, string.Format(Resource.ProjectPlan.Messages.Message_ServeJobTimedOut, 0));

            // It is a 503, as a busy server is, and not tried again: it would only run out of time again.
            server.ApiRequests.ShouldBe(1);
        }

        [Fact]
        public async Task Run_Given_ABusyServerThatStaysBusy_Then_ServerFailureOnceItHasWaitedAsLongAsItMay()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxJobs = 1, MaxQueue = 0 } });
            using HeldContent held = await HeldContent.CreateAsync(RunningServer.CompileContent(File.ReadAllBytes(Plan), @"two-scenarios.zpp"));
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/projects/compile", held);
            await held.Started;
            await Task.Delay(s_Settle);

            var options = new Options { InputFilename = Plan, Server = server.Address };
            ClientSettings settings = ClientSettingsHelper.Resolve(options, Variables()).ShouldNotBeNull();

            // The server says to try again in 5 seconds, which is more than the second zpp may wait - so it gives up at
            // once, rather than wait past its limit. A client that waited past it would wait for the job to end, which it
            // never does: so it is stopped, and fails, rather than hold up the run.
            using var stopped = new CancellationTokenSource(s_Hang);
            var waited = Stopwatch.StartNew();

            try
            {
                (await Should.ThrowAsync<ServerException>(() => JobClient.RunAsync(options, settings, new RecordingJobConsole(), TimeSpan.FromSeconds(1), stopped.Token)))
                    .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServerStayedBusy, server.Address, 1));
                waited.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(4));
            }
            finally
            {
                held.Release();
                (await running).Dispose();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Run_Given_ABusyServerThatSaysWhenToTryAgain_Then_TriesAgainThenAndRunsTheJob(bool asTime)
        {
            // The server says to try again in a second or two - in seconds, or as the time to - which is within the 4
            // seconds zpp may wait, as the 5 seconds zpp waits for a server that does not say are not.
            await using FakeServer server = await FakeServer.StartAsync(
                n => n == 1
                    ? Busy(asTime ? DateTimeOffset.UtcNow.AddSeconds(2).ToString(@"R", CultureInfo.InvariantCulture) : @"1")
                    : Ok());
            var options = new Options { InputFilename = Plan, Server = server.Address };
            ClientSettings settings = ClientSettingsHelper.Resolve(options, Variables()).ShouldNotBeNull();

            (await JobClient.RunAsync(options, settings, new RecordingJobConsole(), TimeSpan.FromSeconds(4))).ShouldBe(ExitCode.Success);

            server.Requests.Count.ShouldBe(2);
        }

        [Fact]
        public async Task Run_Given_AServerThatIsBusyMoreThanOnce_Then_WaitsLongerEachTimeAndSendsTheSameRequestUnderOneTrace()
        {
            await using FakeServer server = await FakeServer.StartAsync(n => n <= 2 ? Busy(@"1") : Ok());
            var options = new Options { InputFilename = Plan, Server = server.Address };
            ClientSettings settings = ClientSettingsHelper.Resolve(options, Variables()).ShouldNotBeNull();

            // A second, then two: more than the two seconds that waiting a second each time would take.
            var waited = Stopwatch.StartNew();
            (await JobClient.RunAsync(options, settings, new RecordingJobConsole(), TimeSpan.FromSeconds(30))).ShouldBe(ExitCode.Success);
            waited.Elapsed.ShouldBeGreaterThan(TimeSpan.FromSeconds(2.9));
            waited.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));

            // The same job each time, in one trace: only the span of each request is its own.
            IReadOnlyList<FakeServer.Request> requests = server.Requests;
            requests.Count.ShouldBe(3);
            requests.Select(x => x.Body.Length).Distinct().Count().ShouldBe(1);
            requests.Select(TraceIdOf).Distinct().Count().ShouldBe(1);
            requests.Select(x => x.Headers[@"traceparent"].Split('-')[2]).Distinct().Count().ShouldBe(3);
        }

        [Fact]
        public async Task Run_Given_ABusyServerWithoutAProblemAndWithoutSayingWhen_Then_StillBusyAndNotRetriedBeyondTheLimit()
        {
            // A 503 from a proxy in front of the server says nothing, and is as busy as the server's own.
            await using FakeServer server = await FakeServer.StartAsync(_ => new FakeServer.Answer(StatusCodes.Status503ServiceUnavailable, @"<html>busy</html>", @"text/html"));
            var options = new Options { InputFilename = Plan, Server = server.Address };
            ClientSettings settings = ClientSettingsHelper.Resolve(options, Variables()).ShouldNotBeNull();

            // The 5 seconds zpp waits for a server that does not say are more than the second it may wait.
            using var stopped = new CancellationTokenSource(s_Hang);
            (await Should.ThrowAsync<ServerException>(() => JobClient.RunAsync(options, settings, new RecordingJobConsole(), TimeSpan.FromSeconds(1), stopped.Token)))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServerStayedBusy, server.Address, 1));
            server.Requests.Count.ShouldBe(1);
        }

        [Fact]
        public async Task Run_Given_ABusyServerThatFreesUp_Then_TriesAgainAndRunsTheJob()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxJobs = 1, MaxQueue = 0 } });
            using HeldContent held = await HeldContent.CreateAsync(RunningServer.CompileContent(File.ReadAllBytes(Plan), @"two-scenarios.zpp"));
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/projects/compile", held);
            await held.Started;
            await Task.Delay(s_Settle);

            var options = new Options { InputFilename = Plan, Server = server.Address };
            ClientSettings settings = ClientSettingsHelper.Resolve(options, Variables()).ShouldNotBeNull();
            var console = new RecordingJobConsole();

            Task<ExitCode> client = JobClient.RunAsync(options, settings, console, TimeSpan.FromSeconds(30));
            await Task.Delay(s_Settle);
            held.Release();
            (await running).Dispose();

            (await client).ShouldBe(ExitCode.Success);
            RecordingJobConsole.Call metrics = console.Calls.ShouldHaveSingleItem();
            metrics.Method.ShouldBe(nameof(RecordingJobConsole.DisplayAsync));
            metrics.HasErrors.ShouldBeFalse();
        }

        [Fact]
        public async Task Run_Given_AnAnswerThatIsNotJson_Then_ServerFailureSayingSo()
        {
            await using FakeServer server = await FakeServer.StartAsync(_ => Ok(@"not an answer"));

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            error.ShouldStartWith(string.Format(Resource.ProjectPlan.Messages.Message_ServerAnswerNotValid, server.Address, string.Empty));
        }

        [Theory]
        [InlineData(@"{}")]
        [InlineData(@"{""metrics"":{},""outputs"":[],""console"":{""exitCode"":0,""standardOutput"":"""",""standardError"":""""}}")]
        [InlineData(@"{""metrics"":{},""outputs"":[],""console"":{""exitCode"":0,""standardOutput"":"""",""standardError"":"""",""transcript"":[{""kind"":""output"",""index"":0}]}}")]
        [InlineData(@"{""metrics"":{},""outputs"":[{""kind"":""project"",""fileName"":""x.zpp"",""contentType"":""application/json"",""content"":""AAAA""}],""console"":{""exitCode"":0,""standardOutput"":"""",""standardError"":"""",""transcript"":[{""kind"":""output"",""index"":0}]}}")]
        [InlineData(@"{""metrics"":{},""outputs"":[{""kind"":""project"",""fileName"":""x.zpp"",""contentType"":""application/json""}],""console"":{""exitCode"":0,""standardOutput"":"""",""standardError"":"""",""transcript"":[{""kind"":""output"",""index"":0}]}}")]
        [InlineData(@"{""metrics"":{},""outputs"":[],""console"":{""exitCode"":0,""standardOutput"":"""",""standardError"":"""",""transcript"":[{""kind"":""line""}]}}")]
        [InlineData(@"{""metrics"":{},""outputs"":[],""console"":{""exitCode"":2,""standardOutput"":"""",""standardError"":"""",""transcript"":[]}}")]
        [InlineData(@"{""metrics"":{},""outputs"":[],""console"":{""exitCode"":1,""standardOutput"":"""",""standardError"":"""",""transcript"":[]}}")]
        public async Task Run_Given_AnAnswerThatDoesNotMatchTheJob_Then_ServerFailureWithoutWritingAnything(string answer)
        {
            // An answer with no console; one with no transcript; one naming an output it does not have; one with an output
            // the job did not ask for; one whose output has no content; a line with no text; an exit code no job ends with;
            // and the exit code of a failure, which is not what a server that answers the job says.
            await using FakeServer server = await FakeServer.StartAsync(_ => Ok(answer));

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerAnswerNotValid,
                server.Address,
                Resource.ProjectPlan.Messages.Message_ServerAnswerDoesNotMatchJob)));
            Directory.GetFiles(m_TempDirectory).ShouldBeEmpty();
        }

        [Theory]
        [InlineData(@"{}")]
        [InlineData(@"{""scenarios"":[],""console"":{""exitCode"":0,""standardOutput"":"""",""standardError"":""""}}")]
        [InlineData(@"{""scenarios"":[],""console"":{""exitCode"":0,""standardOutput"":"""",""standardError"":"""",""transcript"":[{""kind"":""line""}]}}")]
        [InlineData(@"{""scenarios"":[],""console"":{""exitCode"":0,""standardOutput"":"""",""standardError"":"""",""transcript"":[{""kind"":""output"",""index"":0}]}}")]
        [InlineData(@"{""scenarios"":[],""console"":{""exitCode"":3,""standardOutput"":"""",""standardError"":"""",""transcript"":[]}}")]
        public async Task ListScenarios_Given_AnAnswerThatDoesNotMatchTheList_Then_ServerFailure(string answer)
        {
            // An answer with no console; one with no transcript; a line with no text; an output, which a list of scenarios
            // never has; and an exit code no list of scenarios ends with.
            await using FakeServer server = await FakeServer.StartAsync(_ => Ok(answer));

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--list-scenarios", @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerAnswerNotValid,
                server.Address,
                Resource.ProjectPlan.Messages.Message_ServerAnswerDoesNotMatchJob)));
        }

        [Fact]
        public async Task ListScenarios_Given_AnAnswerWithMoreInItThanZppKnows_Then_PrintsItsConsole()
        {
            // From a newer server, with a member this zpp has never heard of.
            await using FakeServer server = await FakeServer.StartAsync(_ => Ok(
                @"{""scenarios"":[],""colour"":""red"",""console"":{""exitCode"":0,""standardOutput"":""x"",""standardError"":"""",""transcript"":[{""kind"":""line"",""text"":""Alpha"",""extra"":1}],""more"":true}}"));

            (int exitCode, string output, _) = await RunAsync([@"-i", Plan, @"--list-scenarios", @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.Success);
            output.ShouldBe("Alpha\n");
        }

        public static TheoryData<ProblemKind, ExitCode> JobProblems => new()
        {
            { ProblemKind.CompilationFailed, ExitCode.CompilationErrors },
            { ProblemKind.CompilationTimedOut, ExitCode.CompilationTimeout },
            { ProblemKind.ProjectNotReadable, ExitCode.Failure },
            { ProblemKind.ScenarioNotSelectable, ExitCode.Failure },
            { ProblemKind.OutputFailed, ExitCode.Failure },
            { ProblemKind.UnexpectedError, ExitCode.Failure },
        };

        [Theory]
        [MemberData(nameof(JobProblems))]
        public async Task Run_Given_AProblemOfAJobThatRanAndFailed_Then_PrintsItsConsoleAndEndsAsTheJobEnded(ProblemKind kind, ExitCode expected)
        {
            await using FakeServer server = await FakeServer.StartAsync(_ => ProblemOf(kind, ConsoleOf(expected, @"The job went wrong.")));

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)expected);
            output.ShouldBeEmpty();
            error.ShouldBe(ErrorLine(@"The job went wrong."));
        }

        [Theory]
        [MemberData(nameof(JobProblems))]
        public async Task ListScenarios_Given_AProblemOfAJobThatRanAndFailed_Then_PrintsItsConsoleAndEndsAsTheJobEnded(ProblemKind kind, ExitCode expected)
        {
            await using FakeServer server = await FakeServer.StartAsync(_ => ProblemOf(kind, ConsoleOf(expected, @"The job went wrong.")));

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--list-scenarios", @"--server", server.Address]);

            exitCode.ShouldBe((int)expected);
            output.ShouldBeEmpty();
            error.ShouldBe(ErrorLine(@"The job went wrong."));
        }

        [Theory]
        [InlineData(ProblemKind.CompilationFailed, ExitCode.Success)]
        [InlineData(ProblemKind.CompilationFailed, ExitCode.Failure)]
        [InlineData(ProblemKind.CompilationTimedOut, ExitCode.CompilationErrors)]
        [InlineData(ProblemKind.ProjectNotReadable, ExitCode.CompilationErrors)]
        [InlineData(ProblemKind.OutputFailed, ExitCode.Success)]
        [InlineData(ProblemKind.UnexpectedError, ExitCode.CompilationTimeout)]
        public async Task Run_Given_AProblemWhoseConsoleDoesNotEndAsItsKindDoes_Then_ServerFailureWithoutPrintingAnything(ProblemKind kind, ExitCode ended)
        {
            await using FakeServer server = await FakeServer.StartAsync(_ => ProblemOf(kind, ConsoleOf(ended, @"The job went wrong.")));

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerAnswerNotValid,
                server.Address,
                Resource.ProjectPlan.Messages.Message_ServerAnswerDoesNotMatchJob)));
        }

        [Theory]
        [InlineData(ProblemKind.CompilationFailed)]
        [InlineData(ProblemKind.ScenarioNotSelectable)]
        [InlineData(ProblemKind.UnexpectedError)]
        public async Task Run_Given_AProblemOfAJobThatRanAndNoConsole_Then_ServerFailureWithItsReason(ProblemKind kind)
        {
            // A server that does not give the console, which zpp always asks for: its problem is all there is to say.
            await using FakeServer server = await FakeServer.StartAsync(_ => ProblemOf(kind, detail: @"It went wrong."));

            (int exitCode, _, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            ShouldBeRefused(error, server.Address, @"It went wrong.", TraceIdOf(server.Requests[0]));
        }

        [Theory]
        [InlineData(ProblemKind.MalformedRequest)]
        [InlineData(ProblemKind.ValidationFailed)]
        public async Task Run_Given_AProblemWithTheRequest_Then_ServerFailureListingEachError(ProblemKind kind)
        {
            // The console, if the server gives it, is not the answer to a job that did not run.
            await using FakeServer server = await FakeServer.StartAsync(_ => ProblemOf(
                kind,
                ConsoleOf(ExitCode.Failure, @"Not printed."),
                @"The request has 2 problems.",
                [
                    new ProblemError { Pointer = @"#/options/scenario", Code = ProblemCodes.WrongType, Detail = @"must be a string" },
                    new ProblemError { Parameter = @"include", Code = ProblemCodes.NotAllowed, Detail = @"is not allowed" },
                ]));

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            ShouldBeRefused(error, server.Address, @"The request has 2 problems: #/options/scenario must be a string; include is not allowed");
        }

        [Fact]
        public async Task Run_Given_AProblemOfATypeZppDoesNotKnow_Then_ServerFailureWithItsReason()
        {
            // As a newer server may have more kinds of problem than this zpp has heard of: a client takes it as its status says.
            var problem = new ProblemResponse
            {
                Type = @"https://example.com/problems#something-new",
                Title = @"Something new",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = @"Something new went wrong.",
                TraceId = new string('a', 32),
                Console = ConsoleOf(ExitCode.Failure, @"Not printed."),
            };
            await using FakeServer server = await FakeServer.StartAsync(_ => new FakeServer.Answer(problem.Status, Json(problem), ProblemHelper.MediaType));

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            ShouldBeRefused(error, server.Address, @"Something new went wrong.");
        }

        [Fact]
        public async Task Run_Given_AnAnswerThatIsNotAProblemAndFailed_Then_ServerFailureWithItsStatus()
        {
            // As a proxy in front of the server answers.
            await using FakeServer server = await FakeServer.StartAsync(_ => new FakeServer.Answer(StatusCodes.Status502BadGateway, @"<html>Bad gateway</html>", @"text/html"));

            (int exitCode, _, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            ShouldBeRefused(error, server.Address, @"502 Bad Gateway", TraceIdOf(server.Requests[0]));
        }

        [Fact]
        public async Task Run_Given_AnAnswerThatIsNotAProblemAndHasNoBody_Then_ServerFailureWithItsStatus()
        {
            await using FakeServer server = await FakeServer.StartAsync(_ => new FakeServer.Answer(StatusCodes.Status404NotFound));

            (int exitCode, _, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            ShouldBeRefused(error, server.Address, @"404 Not Found");
        }

        [Fact]
        public async Task Run_Given_AServerThatSaysWhichRequestItWas_Then_NamesItByItsRequestId()
        {
            // The id the server gives it, which a server that sees a different trace - a proxy's - may not share.
            string requestId = new('b', 32);
            await using FakeServer server = await FakeServer.StartAsync(_ => new FakeServer.Answer(
                StatusCodes.Status404NotFound,
                Headers: new Dictionary<string, string> { [ResponseHeadersMiddleware.RequestIdHeader] = requestId }));

            (int exitCode, _, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            ShouldBeRefused(error, server.Address, @"404 Not Found", requestId);
        }

        [Fact]
        public async Task Run_Given_AnOutputThatCouldNotBeProduced_Then_WritesTheOthersPrintsTheConsoleAndFails()
        {
            // The job ran, and produced the project and not the chart: the project comes with the problem.
            var project = new OutputResponse(JobOutput.Project, @"two-scenarios.zpp", @"application/json", [1, 2, 3]);
            var console = new ConsoleResponse(
                (int)ExitCode.Failure,
                string.Empty,
                "The gantt chart could not be produced.\n",
                [
                    new JobTranscriptEntry { Kind = JobTranscriptKind.Output, Index = 0 },
                    new JobTranscriptEntry { Kind = JobTranscriptKind.ErrorLine, Text = @"The gantt chart could not be produced." },
                ]);
            await using FakeServer server = await FakeServer.StartAsync(_ => ProblemOf(ProblemKind.OutputFailed, console, outputs: [project]));
            string saved = Path.Combine(m_TempDirectory, @"saved.zpp");

            (int exitCode, string output, string error) = await RunAsync(
                [@"-i", Plan, @"-o", saved, @"--gantt-directory", m_TempDirectory, @"--gantt-size", @"800:600", @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.Failure);
            output.ShouldBeEmpty();
            error.ShouldBe(ErrorLine(@"The gantt chart could not be produced."));
            File.ReadAllBytes(saved).ShouldBe([1, 2, 3]);
            Directory.GetFiles(m_TempDirectory).Select(Path.GetFileName).ShouldBe([@"saved.zpp"]);
        }

        [Fact]
        public async Task Run_Given_AJob_Then_SendsTheRequestAsTheContractHasIt()
        {
            await using FakeServer server = await FakeServer.StartAsync(_ => Ok());

            (int exitCode, _, _) = await RunAsync(
                [@"-i", Plan, @"-s", @"Beta", @"--metrics-format", @"json", @"--compile-timeout", @"3500", @"--gantt-directory", m_TempDirectory, @"--gantt-format", @"png", @"--gantt-size", @"800:600", @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.Success);
            FakeServer.Request request = server.Requests.ShouldHaveSingleItem();
            request.Path.ShouldBe(@"/v1/projects/compile");
            request.Query.ShouldBe(@"?include=console");
            request.Headers[@"Accept"].ShouldBe(@"application/json");
            request.Headers.ContainsKey(@"Authorization").ShouldBeFalse();
            request.Headers[@"traceparent"].ShouldMatch(@"^00-[0-9a-f]{32}-[0-9a-f]{16}-00$");

            IReadOnlyList<FakeServer.Part> parts = await request.ReadPartsAsync();
            parts.Select(x => x.Name).ShouldBe([@"project", @"options"]);

            FakeServer.Part project = parts[0];
            project.FileName.ShouldBe(@"two-scenarios.zpp");
            project.ContentType.ShouldBe(@"application/octet-stream");
            project.Content.ShouldBe(File.ReadAllBytes(Plan));

            FakeServer.Part options = parts[1];
            options.FileName.ShouldBeNull();
            options.ContentType.ShouldStartWith(@"application/json");
            Encoding.UTF8.GetString(options.Content).ShouldBe(
                @"{""scenario"":""Beta"",""metricsFormat"":""json"",""compileTimeout"":""PT3.5S"",""outputs"":{""ganttChart"":{""format"":""png"",""width"":800,""height"":600}}}");
        }

        [Fact]
        public async Task Run_Given_AnApiKey_Then_SendsItAsABearerToken()
        {
            await using FakeServer server = await FakeServer.StartAsync(_ => Ok());

            await RunAsync([@"-i", Plan, @"--server", server.Address], Variables((ClientSettingsHelper.ApiKeyVariable, c_ApiKey)));

            server.Requests.ShouldHaveSingleItem().Headers[@"Authorization"].ShouldBe($@"Bearer {c_ApiKey}");
        }

        [Fact]
        public async Task Run_Given_AnImport_Then_SendsTheWorkbookAsTheImportPart()
        {
            await using FakeServer server = await FakeServer.StartAsync(_ => Ok());
            string workbook = Path.Combine(m_TempDirectory, @"plan.xlsx");
            (await ZppMain.RunAsync([@"-i", Plan, @"-x", workbook, @"--now", @"2026-10-02T09:00:00+01:00"])).ShouldBe(0);

            (int exitCode, _, _) = await RunAsync([@"-m", workbook, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.Success);
            IReadOnlyList<FakeServer.Part> parts = await server.Requests.ShouldHaveSingleItem().ReadPartsAsync();
            parts.Select(x => x.Name).ShouldBe([@"import", @"options"]);
            parts[0].FileName.ShouldBe(@"plan.xlsx");
        }

        [Fact]
        public async Task ListScenarios_Given_AProject_Then_SendsOnlyTheProject()
        {
            await using FakeServer server = await FakeServer.StartAsync(_ => Ok(@"{""scenarios"":[],""console"":{""exitCode"":0,""standardOutput"":"""",""standardError"":"""",""transcript"":[]}}"));

            (int exitCode, _, _) = await RunAsync([@"-i", Plan, @"--list-scenarios", @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.Success);
            FakeServer.Request request = server.Requests.ShouldHaveSingleItem();
            request.Path.ShouldBe(@"/v1/projects/scenarios");
            request.Query.ShouldBe(@"?include=console");
            (await request.ReadPartsAsync()).Select(x => x.Name).ShouldBe([@"project"]);
        }

        [Theory]
        [InlineData(@"plan.mpp")]
        [InlineData(@"plan.xml")]
        public async Task Run_Given_AnMsProjectImportAndAServer_Then_UsageErrorSayingToRunItHere(string import)
        {
            (int exitCode, _, string error) = await RunAsync([@"-m", import, @"--server", AddressNothingListensOn()]);

            exitCode.ShouldBe((int)ExitCode.UsageError);
            error.ShouldBe(ErrorLine(string.Format(Resource.ProjectPlan.Messages.Message_ServerCannotImport, import, @"--server")));
        }

        [Fact]
        public async Task Run_Given_AnMsProjectImportAndTheServerInTheEnvironment_Then_UsageErrorSayingToUseLocal()
        {
            (int exitCode, _, string error) = await RunAsync(
                [@"-m", @"plan.mpp"],
                Variables((ClientSettingsHelper.ServerVariable, AddressNothingListensOn())));

            exitCode.ShouldBe((int)ExitCode.UsageError);
            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerFromEnvironmentCannotImport,
                ClientSettingsHelper.ServerVariable,
                @"plan.mpp",
                @"--local")));
        }

        [Fact]
        public async Task Run_Given_LocalAndTheServerInTheEnvironment_Then_RunsHere()
        {
            // Nothing listens there, so the run can only succeed here.
            (int exitCode, string output, _) = await RunAsync(
                [@"-i", Plan, @"--local"],
                Variables((ClientSettingsHelper.ServerVariable, AddressNothingListensOn())));

            exitCode.ShouldBe((int)ExitCode.Success);
            output.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task Run_Given_ABlankServerInTheEnvironment_Then_RunsHere()
        {
            (int exitCode, _, _) = await RunAsync([@"-i", Plan], Variables((ClientSettingsHelper.ServerVariable, @"  ")));

            exitCode.ShouldBe((int)ExitCode.Success);
        }

        [Fact]
        public async Task Run_Given_ServerWithoutItsAddress_Then_UsageErrorRatherThanARunHere()
        {
            // The parser would drop --server at the end, and the run would be here.
            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server"]);

            exitCode.ShouldBe((int)ExitCode.UsageError);
            output.ShouldBeEmpty();
            error.ShouldBe(ErrorLine(string.Format(Resource.ProjectPlan.Messages.Message_OptionNeedsValue, @"--server")));
        }

        [Fact]
        public async Task Run_Given_ServerAndLocal_Then_UsageError()
        {
            (int exitCode, _, string error) = await RunAsync([@"-i", Plan, @"--server", AddressNothingListensOn(), @"--local"]);

            exitCode.ShouldBe((int)ExitCode.UsageError);
            error.ShouldBe(ErrorLine(string.Format(Resource.ProjectPlan.Messages.Message_SpecifyEitherOptionNotBoth, @"--server", @"--local")));
        }

        [Fact]
        public async Task Run_Given_AnApiKeyFileAndNoServer_Then_UsageError()
        {
            (int exitCode, _, string error) = await RunAsync([@"-i", Plan, @"--api-key-file", @"api-key"]);

            exitCode.ShouldBe((int)ExitCode.UsageError);
            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_OptionOnlyValidWithServer,
                @"--api-key-file",
                @"--server",
                ClientSettingsHelper.ServerVariable)));
        }

        [Theory]
        [InlineData(@"ftp://localhost:9770")]
        [InlineData(@"localhost:9770")]
        [InlineData(@"http://user:password@localhost:9770")]
        [InlineData(@"http://localhost:9770/?job=1")]
        [InlineData(@"unix:")]
        [InlineData(@"")]
        public async Task Run_Given_AServerThatIsNotOne_Then_UsageErrorNamingIt(string server)
        {
            (int exitCode, _, string error) = await RunAsync([@"-i", Plan, @"--server", server]);

            exitCode.ShouldBe((int)ExitCode.UsageError);
            error.ShouldBe(ErrorLine(string.Format(Resource.ProjectPlan.Messages.Message_ServerNotValid, server, @"--server")));
        }

        [Fact]
        public async Task Run_Given_AServerInTheEnvironmentThatIsNotOne_Then_UsageErrorNamingTheVariable()
        {
            (int exitCode, _, string error) = await RunAsync(
                [@"-i", Plan],
                Variables((ClientSettingsHelper.ServerVariable, @"nonsense")));

            exitCode.ShouldBe((int)ExitCode.UsageError);
            error.ShouldBe(ErrorLine(string.Format(Resource.ProjectPlan.Messages.Message_ServerNotValid, @"nonsense", ClientSettingsHelper.ServerVariable)));
        }

        [Fact]
        public async Task Run_Given_WhatZppRefuses_Then_RefusesItAsZppDoesWithoutSendingAnything()
        {
            // A usage error, a plan that is not there, a directory that is not there, and an export to a file zpp does not
            // write - which the server, sent no file name, would export to.
            await using RunningServer server = await StartAsync();
            string missing = Path.Combine(m_TempDirectory, @"missing");
            string export = Path.Combine(m_TempDirectory, @"exported.txt");

            (int usage, _, _) = await RunAsync([@"-i", Plan, @"--server", server.Address, @"--compile-timeout", @"-1"]);
            (int noPlan, _, string noPlanError) = await RunAsync([@"-i", Path.Combine(missing, @"plan.zpp"), @"--server", server.Address]);
            (int noDirectory, _, string noDirectoryError) = await RunAsync(
                [@"-i", Plan, @"--server", server.Address, @"--arrow-directory", missing]);
            (int badExport, _, string badExportError) = await RunAsync([@"-i", Plan, @"--server", server.Address, @"-x", export]);
            (int badExportHere, _, string badExportHereError) = await RunAsync([@"-i", Plan, @"-x", export]);

            usage.ShouldBe((int)ExitCode.UsageError);
            noPlan.ShouldBe((int)ExitCode.Failure);
            noPlanError.ShouldContain(missing);
            noDirectory.ShouldBe((int)ExitCode.Failure);
            noDirectoryError.ShouldBe(ErrorLine(string.Format(Resource.ProjectPlan.Messages.Message_DirectoryDoesNotExist, missing)));
            badExportHere.ShouldBe((int)ExitCode.Failure);
            badExport.ShouldBe(badExportHere);
            badExportError.ShouldBe(badExportHereError);
            File.Exists(export).ShouldBeFalse();
            server.ApiRequests.ShouldBe(0);
        }

        [Fact]
        public async Task Run_Given_Verbose_Then_SaysWhereTheJobRanAndTheRequestsId()
        {
            await using RunningServer server = await StartAsync();

            (int exitCode, _, string error) = await ZppMain.RunCapturedAsync([@"-i", Plan, @"--server", server.Address, @"-v"]);

            exitCode.ShouldBe((int)ExitCode.Success);
            error.ShouldContain($@"Running two-scenarios.zpp on {server.Address}");
            error.ShouldMatch($@"Request [0-9a-f]{{32}} ran on {Regex.Escape(server.Address)}: exit code 0");
        }
    }
}
