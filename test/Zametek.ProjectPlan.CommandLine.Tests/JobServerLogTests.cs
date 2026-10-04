using Serilog;
using Shouldly;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp serve's log: what a line of it looks like in each format, with the id of the request it belongs to when it
    /// belongs to one - which is the id the response says - and what a request to the server leaves in it. In the same
    /// collection as ProgramExitCodeTests, because the log is written to the console's stderr, which they swap.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class JobServerLogTests
        : IClassFixture<EngineFixture>
    {
        private const string c_TraceId = @"4bf92f3577b34da6a3ce929d0e0e4736";

        private readonly EngineFixture m_Engine;

        public JobServerLogTests(EngineFixture engine)
        {
            m_Engine = engine;
        }

        // What the log writes on stderr while the action runs.
        private static async Task<string> CaptureAsync(Func<Task> action)
        {
            TextWriter original = Console.Error;

            try
            {
                using var error = new StringWriter();
                Console.SetError(error);
                await action();
                return error.ToString();
            }
            finally
            {
                Console.SetError(original);
            }
        }

        private static Task<string> WriteAsync(
            LogFormat format,
            bool inARequest)
        {
            return CaptureAsync(() =>
            {
                using Serilog.Core.Logger logger = JobServer.CreateLogger(verbose: false, format);
                using Activity? request = inARequest ? StartRequest() : null;
                logger.Information(@"{Method} {Path}: {StatusCode}", @"POST", @"/v1/projects/compile", 200);
                return Task.CompletedTask;
            });
        }

        private static Activity StartRequest()
        {
            var request = new Activity(@"request");
            request.SetIdFormat(ActivityIdFormat.W3C);
            request.SetParentId(ActivityTraceId.CreateFromString(c_TraceId), ActivitySpanId.CreateFromString(@"00f067aa0ba902b7"));
            return request.Start();
        }

        [Fact]
        public async Task CreateLogger_Given_TextInARequest_Then_ALineThatSaysTheRequest()
        {
            string line = await WriteAsync(LogFormat.Text, inARequest: true);

            line.ShouldMatch($@"^\[\d\d:\d\d:\d\d INF\] {c_TraceId} POST /v1/projects/compile: 200\r?\n$");
        }

        [Fact]
        public async Task CreateLogger_Given_TextOutsideARequest_Then_ALineWithNoIdAndNoGapWhereItWouldBe()
        {
            string line = await WriteAsync(LogFormat.Text, inARequest: false);

            line.ShouldMatch(@"^\[\d\d:\d\d:\d\d INF\] POST /v1/projects/compile: 200\r?\n$");
        }

        [Fact]
        public async Task CreateLogger_Given_JsonInARequest_Then_AnObjectWithTheTraceId()
        {
            string line = await WriteAsync(LogFormat.Json, inARequest: true);

            using JsonDocument document = JsonDocument.Parse(line);
            document.RootElement.GetProperty(@"level").GetString().ShouldBe(@"information");
            document.RootElement.GetProperty(@"message").GetString().ShouldBe(@"POST /v1/projects/compile: 200");
            document.RootElement.GetProperty(@"traceId").GetString().ShouldBe(c_TraceId);
            document.RootElement.GetProperty(@"properties").GetProperty(@"statusCode").GetInt32().ShouldBe(200);
            document.RootElement.GetProperty(@"properties").EnumerateObject().Select(x => x.Name).ShouldBe([@"method", @"path", @"statusCode"]);
            line.Count(x => x == '\n').ShouldBe(1);
        }

        [Fact]
        public async Task CreateLogger_Given_JsonOutsideARequest_Then_NoTraceId()
        {
            using JsonDocument document = JsonDocument.Parse(await WriteAsync(LogFormat.Json, inARequest: false));

            document.RootElement.TryGetProperty(@"traceId", out _).ShouldBeFalse();
        }

        [Fact]
        public async Task CreateLogger_Given_TheLevelsOfTheWebServer_Then_ItsInformationIsLeftOutUnlessVerbose()
        {
            string quiet = await CaptureAsync(() =>
            {
                using Serilog.Core.Logger logger = JobServer.CreateLogger(verbose: false, LogFormat.Text);
                logger.ForContext(typeof(Microsoft.AspNetCore.Builder.WebApplication)).Information(@"informational");
                logger.ForContext(typeof(Microsoft.AspNetCore.Builder.WebApplication)).Warning(@"warning");
                return Task.CompletedTask;
            });
            string verbose = await CaptureAsync(() =>
            {
                using Serilog.Core.Logger logger = JobServer.CreateLogger(verbose: true, LogFormat.Text);
                logger.ForContext(typeof(Microsoft.AspNetCore.Builder.WebApplication)).Information(@"informational");
                return Task.CompletedTask;
            });

            quiet.ShouldNotContain(@"informational");
            quiet.ShouldContain(@"warning");
            verbose.ShouldContain(@"informational");
        }

        [Theory]
        [InlineData(LogFormat.Text)]
        [InlineData(LogFormat.Json)]
        public async Task Log_Given_ARequestWithATraceparent_Then_EveryLineOfItNamesTheTraceTheResponseSays(LogFormat format)
        {
            ILogger previous = Log.Logger;
            string log;
            string requestId = string.Empty;

            try
            {
                log = await CaptureAsync(async () =>
                {
                    Log.Logger = JobServer.CreateLogger(verbose: false, format);
                    await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

                    using var content = RunningServer.CompileContent(
                        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, @"Assets", @"two-scenarios.zpp")),
                        @"two-scenarios.zpp");
                    using var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/projects/compile") { Content = content };
                    request.Headers.Add(@"traceparent", $@"00-{c_TraceId}-00f067aa0ba902b7-01");
                    using HttpResponseMessage response = await server.Client.SendAsync(request);
                    requestId = response.Headers.GetValues(ResponseHeadersMiddleware.RequestIdHeader).ShouldHaveSingleItem();
                });
            }
            finally
            {
                (Log.Logger as IDisposable)?.Dispose();
                Log.Logger = previous;
            }

            requestId.ShouldBe(c_TraceId);

            // The line of the job it ran, which names the trace; and no line of its trace is without it.
            string[] lines = [.. log.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.TrimEnd('\r')).Where(x => x.Contains(@"/v1/projects/compile"))];
            lines.ShouldNotBeEmpty();
            lines.ShouldAllBe(x => x.Contains(c_TraceId));

            if (format == LogFormat.Json)
            {
                using JsonDocument document = JsonDocument.Parse(lines[0]);
                document.RootElement.GetProperty(@"traceId").GetString().ShouldBe(c_TraceId);
                document.RootElement.GetProperty(@"properties").GetProperty(@"statusCode").GetInt32().ShouldBe(200);
            }
            else
            {
                lines[0].ShouldMatch($@"^\[\d\d:\d\d:\d\d INF\] {c_TraceId} POST /v1/projects/compile: 200 ok, exit code 0, after \d+ ms$");
            }
        }

        [Fact]
        public async Task Log_Given_ARefusedKey_Then_ALineWithTheTraceAndNoKey()
        {
            ILogger previous = Log.Logger;
            const string key = @"0123456789abcdef0123456789abcdef";
            string log;

            try
            {
                log = await CaptureAsync(async () =>
                {
                    Log.Logger = JobServer.CreateLogger(verbose: false, LogFormat.Text);
                    await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { ApiKey = key });

                    using var request = new HttpRequestMessage(HttpMethod.Get, @"/v1/info");
                    request.Headers.Add(@"traceparent", $@"00-{c_TraceId}-00f067aa0ba902b7-01");
                    request.Headers.Authorization = new AuthenticationHeaderValue(@"Bearer", @"not-the-key-not-the-key-not-the-key");
                    using HttpResponseMessage response = await server.Client.SendAsync(request);
                    response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
                });
            }
            finally
            {
                (Log.Logger as IDisposable)?.Dispose();
                Log.Logger = previous;
            }

            Regex.Matches(log, $@"\[\d\d:\d\d:\d\d WRN\] {c_TraceId} GET /v1/info: refused, API key not accepted").Count.ShouldBe(1);
            log.ShouldNotContain(key);
            log.ShouldNotContain(@"not-the-key");
        }
    }
}
