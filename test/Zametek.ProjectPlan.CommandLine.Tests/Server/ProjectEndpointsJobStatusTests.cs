using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// What zpp serve answers to a caller who asks how far its job has got: GET /v1/jobs/{requestId}, where the id is the trace id
    /// that the request of the job gave in its traceparent. The status is there while the job runs, and not before or after.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class ProjectEndpointsJobStatusTests
        : IClassFixture<EngineFixture>
    {
        private const string c_TraceId = "0af7651916cd43dd8448eb211c80319c";
        private const string c_ApiKey = "s3cr3t-k3y";

        private readonly EngineFixture m_Engine;

        public ProjectEndpointsJobStatusTests(EngineFixture engine)
        {
            m_Engine = engine;
        }

        // The sample plan, saved with the given number of iterations as the settings of its simulation.
        private async Task<byte[]> SampleWithIterationsAsync(int iterations)
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
                new JobRequest { Input = input, SaveProject = true, SimulationSettings = new SimulationSettingsModel { Iterations = iterations } },
                sink);

            return sink.Outputs.Single(x => x.Output == JobOutput.Project).Content;
        }

        private static HttpRequestMessage Get(string id, string? accept = null, string? apiKey = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $@"/v1/jobs/{id}");

            if (accept is not null)
            {
                request.Headers.TryAddWithoutValidation(@"Accept", accept);
            }

            if (apiKey is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(@"Bearer", apiKey);
            }

            return request;
        }

        // The endpoints, built by hand with a logger that keeps what it is told, for a test of what they log.
        private ProjectEndpoints Endpoints(
            RecordingLogger<ProjectEndpoints> logger,
            RunningJobs jobs)
        {
            return new ProjectEndpoints(m_Engine.JobRunner, new ServeLimits(), jobs, TimeProvider.System, logger);
        }

        // A request for the status of a job, which no web server has read: the id is the last part of its path, as it came.
        private static DefaultHttpContext Request(
            ServiceProvider services,
            string id)
        {
            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = $@"/v1/jobs/{id}";
            context.Request.RouteValues[@"requestId"] = id;
            context.Response.Body = new MemoryStream();
            return context;
        }

        // A request to run the simulation of a plan, in the trace with the given id.
        private static HttpRequestMessage Compile(byte[] plan, string traceId)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/projects/compile")
            {
                Content = RunningServer.CompileContent(plan, @"sample.zpp", new CompileOptions { RunSimulation = true }),
            };
            request.Headers.Add(@"traceparent", $@"00-{traceId}-00f067aa0ba902b7-00");
            return request;
        }

        [Fact]
        public async Task GetJob_Given_AJobThatRuns_Then_AnswersWithItsStatusAndHowFarItHasGot()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { Limits = new ServeLimits { MaxSimulationIterations = 1_000_000 } });
            byte[] plan = await SampleWithIterationsAsync(1_000_000);
            using var cancellation = new CancellationTokenSource();

            // A run of a million iterations takes minutes, and the test stops it once it has seen what it came for.
            Task<HttpResponseMessage> job = server.Client.SendAsync(Compile(plan, c_TraceId), cancellation.Token);
            JobStatusResponse? seen = null;
            var waited = Stopwatch.StartNew();

            while (seen?.Progress is null
                && waited.Elapsed < TimeSpan.FromSeconds(120))
            {
                using HttpResponseMessage response = await server.Client.SendAsync(Get(c_TraceId));

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    response.Headers.RetryAfter.ShouldNotBeNull().Delta.ShouldBe(TimeSpan.FromSeconds(JobServer.PollAfterSeconds));
                    response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
                    response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe(@"application/json");
                    seen = await RunningServer.ReadAsync<JobStatusResponse>(response);
                }
                else
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
                }

                await Task.Delay(50);
            }

            seen.ShouldNotBeNull();
            seen.Id.ShouldBe(c_TraceId);
            seen.State.ShouldBe(JobState.Running);
            seen.Elapsed.ShouldBeGreaterThan(TimeSpan.Zero);
            JobProgressResponse progress = seen.Progress.ShouldNotBeNull();
            progress.Total.ShouldBe(1_000_000);
            progress.Done.ShouldBeInRange(1, 1_000_000);

            // The job ends when the caller leaves, and then it has no status.
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => job);

            var gone = Stopwatch.StartNew();
            HttpStatusCode status;
            do
            {
                using HttpResponseMessage response = await server.Client.SendAsync(Get(c_TraceId));
                status = response.StatusCode;

                if (status != HttpStatusCode.NotFound)
                {
                    await Task.Delay(100);
                }
            }
            while (status != HttpStatusCode.NotFound && gone.Elapsed < TimeSpan.FromSeconds(120));

            status.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetJob_Given_AnIdNoJobHas_Then_404WithAProblem()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            using HttpResponseMessage response = await server.Client.SendAsync(Get(c_TraceId));

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);
            problem.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeJobNotRunning);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("0af7651916cd43dd8448eb211c80319")]
        [InlineData("0af7651916cd43dd8448eb211c80319cc")]
        [InlineData("0af7651916cd43dd8448eb211c80319g")]
        public async Task GetJob_Given_AnIdThatIsNotATraceId_Then_400WithTheParameterNamed(string id)
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            using HttpResponseMessage response = await server.Client.SendAsync(Get(id));

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Parameter.ShouldBe(@"requestId");
            error.Code.ShouldBe(ProblemCodes.InvalidFormat);
            error.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeErrorJobIdNotValid);
        }

        [Fact]
        public async Task GetJob_Given_AnAcceptThatIsNotJson_Then_406()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            using HttpResponseMessage response = await server.Client.SendAsync(Get(c_TraceId, accept: @"text/html"));

            response.StatusCode.ShouldBe(HttpStatusCode.NotAcceptable);
        }

        // The last part of the path is whatever the caller sent, and a line of the log must not end in it.
        [Theory]
        [InlineData("a\nWARN fake line", StatusCodes.Status400BadRequest)]
        [InlineData("a\r\n[12:00:00 WRN] fake line", StatusCodes.Status400BadRequest)]
        [InlineData("abc", StatusCodes.Status400BadRequest)]
        [InlineData(c_TraceId, StatusCodes.Status404NotFound)]
        public async Task GetJob_Given_AnId_Then_LogsTheRouteAndNotThePath(string id, int status)
        {
            var logger = new RecordingLogger<ProjectEndpoints>();
            await using ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
            DefaultHttpContext context = Request(services, id);

            await Endpoints(logger, new RunningJobs(TimeProvider.System)).GetJobAsync(context);

            context.Response.StatusCode.ShouldBe(status);
            logger.Entries.ShouldHaveSingleItem().ShouldBe((LogLevel.Debug, $@"GET /v1/jobs/{{requestId}}: {status}"));
        }

        [Fact]
        public async Task GetJob_Given_AJobThatRuns_Then_LogsTheRouteAndNotThePath()
        {
            var logger = new RecordingLogger<ProjectEndpoints>();
            var jobs = new RunningJobs(TimeProvider.System);
            using RunningJobs.Registration job = jobs.Begin(c_TraceId);
            await using ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
            DefaultHttpContext context = Request(services, c_TraceId);

            await Endpoints(logger, jobs).GetJobAsync(context);

            context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
            logger.Entries.ShouldHaveSingleItem().ShouldBe((LogLevel.Debug, @"GET /v1/jobs/{requestId}: 200"));
        }

        [Fact]
        public async Task GetJob_Given_AServerThatNeedsAKey_Then_RefusesACallerWithoutIt()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { ApiKey = c_ApiKey });

            using HttpResponseMessage without = await server.Client.SendAsync(Get(c_TraceId));
            using HttpResponseMessage with = await server.Client.SendAsync(Get(c_TraceId, apiKey: c_ApiKey));

            without.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            with.StatusCode.ShouldBe(HttpStatusCode.NotFound); // Past the key: the job is not there.
        }

        [Fact]
        public async Task GetJob_Given_AServerWhoseJobsAllRun_Then_StillAnswers()
        {
            // The question is not a job: a caller who asks how far its job has got must not wait for a place among the jobs.
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { Limits = new ServeLimits { MaxJobs = 1, MaxQueue = 0 } });
            using HeldContent held = await HeldContent.CreateAsync(RunningServer.CompileContent(await SampleWithIterationsAsync(10), @"sample.zpp"));
            Task<HttpResponseMessage> running = server.Client.PostAsync(@"/v1/projects/compile", held);
            await held.Started;
            await Task.Delay(250);

            using HttpResponseMessage response = await server.Client.SendAsync(Get(c_TraceId));

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            held.Release();
            using HttpResponseMessage ran = await running;
            ran.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Options_Given_TheJobStatus_Then_SaysItTakesGet()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            using HttpResponseMessage response = await server.Client.SendAsync(new HttpRequestMessage(HttpMethod.Options, $@"/v1/jobs/{c_TraceId}"));

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            response.Content.Headers.Allow.ShouldBe(["GET", "OPTIONS"], ignoreOrder: true);
        }
    }
}
