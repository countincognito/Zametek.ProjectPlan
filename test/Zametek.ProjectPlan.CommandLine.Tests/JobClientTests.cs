using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp --server around the job: where it runs - on --server, on
    /// ZPP_SERVER unless --local says otherwise, or here - and what it refuses
    /// before anything is sent; a server that does not run the job - one that
    /// is not there or not trusted, wants its API key, is asked for more than
    /// its limits allow, stays busy for longer than zpp may wait, runs out of
    /// time, or answers with something zpp cannot read - which ends the run
    /// with exit code 5, saying why; and one that is busy for a while, which
    /// zpp tries again when it says to. What a job a server runs prints and
    /// writes - as zpp would - JobClientParityTests pins. In the same
    /// collection as ProgramExitCodeTests, because Main swaps the console's
    /// streams while it runs.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class JobClientTests
        : IClassFixture<EngineFixture>, IDisposable
    {
        private const string c_ApiKey = @"s3cr3t-k3y";

        // The answer to a job that printed and produced nothing, and succeeded.
        private const string c_NothingToDoAnswer = @"{""jobId"":""1"",""exitCode"":0,""stdout"":"""",""stderr"":"""",""outputs"":[],""transcript"":[]}";

        // How long a job held part way through its upload is given to reach the server, before another is sent.
        private static readonly TimeSpan s_Settle = TimeSpan.FromMilliseconds(250);

        // A line a log wrote on stderr.
        private static readonly Regex s_LogLine = new(@"^\[\d\d:\d\d:\d\d [A-Z]{3}\] .*(\r?\n|$)", RegexOptions.Multiline);

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

        // A server that answers every job, and every request for a plan's scenarios, with the same text, whatever it is
        // sent - once it has said it is busy, and when to try again, as many times as it is told to.
        private static async Task<WebApplication> StartAnsweringAsync(
            string answer,
            int busy = 0,
            Func<string>? retryAfter = null)
        {
            WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
            builder.WebHost.UseKestrelCore();
            builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
            builder.Services.AddRoutingCore();

            WebApplication app = builder.Build();
            int requests = 0;

            async Task AnswerAsync(HttpContext context)
            {
                if (Interlocked.Increment(ref requests) <= busy)
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    context.Response.Headers.RetryAfter = retryAfter?.Invoke();
                    return;
                }

                context.Response.ContentType = @"application/json";
                await context.Response.WriteAsync(answer);
            }

            app.MapPost(@"/v1/jobs", (RequestDelegate)AnswerAsync);
            app.MapPost(@"/v1/scenarios", (RequestDelegate)AnswerAsync);

            await app.StartAsync();
            return app;
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
        public async Task Run_Given_AChartBeyondTheServersLimits_Then_ServerFailureWithTheServersReason()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxChartWidth = 300 } });

            (int exitCode, _, string error) = await RunAsync(
                [@"-i", Plan, @"--server", server.Address, @"--gantt-directory", m_TempDirectory, @"--gantt-size", @"800:600"]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerRefused,
                server.Address,
                string.Format(Resource.ProjectPlan.Messages.Message_ServeChartSizeOutOfRange, @"gantt", 300, new ServeLimits().MaxChartHeight))));
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
            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerRefused,
                server.Address,
                string.Format(Resource.ProjectPlan.Messages.Message_ServeRequestTooLarge, 1))));
        }

        [Fact]
        public async Task Run_Given_AJobThatRunsOutOfTimeOnTheServer_Then_ServerFailureWithTheServersReason()
        {
            // A limit of no time at all is one no job can keep to.
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { JobTimeoutSeconds = 0 } });

            (int exitCode, _, string error) = await RunAsync([@"-i", Plan, @"--server", server.Address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerRefused,
                server.Address,
                string.Format(Resource.ProjectPlan.Messages.Message_ServeJobTimedOut, 0))));
        }

        [Fact]
        public async Task Run_Given_ABusyServerThatStaysBusy_Then_ServerFailureOnceItHasWaitedAsLongAsItMay()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxJobs = 1, MaxQueue = 0 } });
            using HeldContent held = await HeldContent.CreateAsync(RunningServer.JobContent(File.ReadAllBytes(Plan), @"two-scenarios.zpp"));
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/jobs", held);
            await held.Started;
            await Task.Delay(s_Settle);

            var options = new Options { InputFilename = Plan, Server = server.Address };
            ClientSettings settings = ClientSettingsHelper.Resolve(options, Variables()).ShouldNotBeNull();

            // The server says to try again in 5 seconds, which is more than the second zpp may wait - so it gives up at
            // once, rather than wait past its limit.
            var waited = Stopwatch.StartNew();
            (await Should.ThrowAsync<ServerException>(() => JobClient.RunAsync(options, settings, new RecordingJobConsole(), TimeSpan.FromSeconds(1))))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServerStayedBusy, server.Address, 1));
            waited.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(4));

            held.Release();
            (await running).Dispose();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Run_Given_ABusyServerThatSaysWhenToTryAgain_Then_TriesAgainThenAndRunsTheJob(bool asTime)
        {
            // The server says to try again in a second or two - in seconds, or as the time to - which is within the 4
            // seconds zpp may wait, as the 5 seconds zpp waits for a server that does not say are not.
            await using WebApplication app = await StartAnsweringAsync(
                c_NothingToDoAnswer,
                busy: 1,
                retryAfter: () => asTime ? DateTimeOffset.UtcNow.AddSeconds(2).ToString(@"R", CultureInfo.InvariantCulture) : @"1");
            var options = new Options { InputFilename = Plan, Server = app.Urls.First() };
            ClientSettings settings = ClientSettingsHelper.Resolve(options, Variables()).ShouldNotBeNull();

            (await JobClient.RunAsync(options, settings, new RecordingJobConsole(), TimeSpan.FromSeconds(4))).ShouldBe(ExitCode.Success);
        }

        [Fact]
        public async Task Run_Given_ABusyServerThatFreesUp_Then_TriesAgainAndRunsTheJob()
        {
            await using RunningServer server = await StartAsync(new ServeSettings { Limits = new ServeLimits { MaxJobs = 1, MaxQueue = 0 } });
            using HeldContent held = await HeldContent.CreateAsync(RunningServer.JobContent(File.ReadAllBytes(Plan), @"two-scenarios.zpp"));
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/jobs", held);
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
            await using WebApplication app = await StartAnsweringAsync(@"not an answer");
            string address = app.Urls.First();

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server", address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            error.ShouldStartWith(string.Format(Resource.ProjectPlan.Messages.Message_ServerAnswerNotValid, address, string.Empty));
        }

        [Theory]
        [InlineData(@"{}")]
        [InlineData(@"{""jobId"":""1"",""exitCode"":0,""stdout"":"""",""stderr"":"""",""outputs"":[]}")]
        [InlineData(@"{""jobId"":""1"",""exitCode"":0,""stdout"":"""",""stderr"":"""",""outputs"":[],""transcript"":[{""kind"":""output"",""index"":0}]}")]
        [InlineData(@"{""jobId"":""1"",""exitCode"":0,""stdout"":"""",""stderr"":"""",""outputs"":[{""kind"":""project"",""fileName"":""x.zpp"",""contentType"":""application/json"",""content"":""AAAA""}],""transcript"":[{""kind"":""output"",""index"":0}]}")]
        [InlineData(@"{""jobId"":""1"",""exitCode"":0,""stdout"":"""",""stderr"":"""",""outputs"":[],""transcript"":[{""kind"":""line""}]}")]
        [InlineData(@"{""jobId"":""1"",""exitCode"":2,""stdout"":"""",""stderr"":"""",""outputs"":[],""transcript"":[]}")]
        public async Task Run_Given_AnAnswerThatDoesNotMatchTheJob_Then_ServerFailureWithoutWritingAnything(string answer)
        {
            // An answer with no transcript; one naming an output it does not have; one with an output the job did not
            // ask for; a line with no text; and an exit code no job ends with.
            await using WebApplication app = await StartAnsweringAsync(answer);
            string address = app.Urls.First();

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--server", address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerAnswerNotValid,
                address,
                Resource.ProjectPlan.Messages.Message_ServerAnswerDoesNotMatchJob)));
            Directory.GetFiles(m_TempDirectory).ShouldBeEmpty();
        }

        [Theory]
        [InlineData(@"{}")]
        [InlineData(@"{""jobId"":""1"",""exitCode"":0,""stdout"":"""",""stderr"":"""",""scenarios"":[],""transcript"":[{""kind"":""line""}]}")]
        [InlineData(@"{""jobId"":""1"",""exitCode"":0,""stdout"":"""",""stderr"":"""",""scenarios"":[],""transcript"":[{""kind"":""output"",""index"":0}]}")]
        [InlineData(@"{""jobId"":""1"",""exitCode"":3,""stdout"":"""",""stderr"":"""",""scenarios"":[],""transcript"":[]}")]
        public async Task ListScenarios_Given_AnAnswerThatDoesNotMatchTheList_Then_ServerFailure(string answer)
        {
            // An answer with no transcript; a line with no text; an output, which a list of scenarios never has; and an
            // exit code no list of scenarios ends with.
            await using WebApplication app = await StartAnsweringAsync(answer);
            string address = app.Urls.First();

            (int exitCode, string output, string error) = await RunAsync([@"-i", Plan, @"--list-scenarios", @"--server", address]);

            exitCode.ShouldBe((int)ExitCode.ServerFailure);
            output.ShouldBeEmpty();
            error.ShouldBe(ErrorLine(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerAnswerNotValid,
                address,
                Resource.ProjectPlan.Messages.Message_ServerAnswerDoesNotMatchJob)));
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
        public async Task Run_Given_Verbose_Then_SaysWhereTheJobRan()
        {
            await using RunningServer server = await StartAsync();

            (int exitCode, _, string error) = await ZppMain.RunCapturedAsync([@"-i", Plan, @"--server", server.Address, @"-v"]);

            exitCode.ShouldBe((int)ExitCode.Success);
            error.ShouldContain($@"Running two-scenarios.zpp on {server.Address}");
            error.ShouldContain($@"ran on {server.Address}: exit code 0");
        }
    }
}
