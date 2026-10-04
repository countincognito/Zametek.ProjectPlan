using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Shouldly;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // A zpp serve started for a test, as JobServer builds it - by default on a port of its own on this machine, chosen
    // when it starts - with a client for it, and a count of the requests its API has been sent, so that a test can tell
    // a run sent to it from one that never reached it.
    internal sealed class RunningServer
        : IAsyncDisposable
    {
        private readonly WebApplication m_App;
        private HttpClient? m_Client;
        private int m_ApiRequests;

        private RunningServer(WebApplication app)
        {
            m_App = app;
        }

        public HttpClient Client => m_Client ?? throw new InvalidOperationException();

        public string Address => m_App.Urls.First();

        // The requests the API has taken - past its API key and its limits.
        public int ApiRequests => Volatile.Read(ref m_ApiRequests);

        // The routes the server maps, each with a method it takes - GET, for the health checks, which take any - as the
        // description of the API is held to them.
        public IReadOnlyList<(string Method, string Path)> Routes => [.. ((IEndpointRouteBuilder)m_App).DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [@"GET"])
                .Select(method => (method, endpoint.RoutePattern.RawText ?? string.Empty)))];

        public static async Task<RunningServer> StartAsync(
            JobRunner jobRunner,
            ServeSettings? settings = null,
            bool warmUp = false,
            HttpMessageHandler? handler = null)
        {
            settings ??= new ServeSettings();

            if (settings.Listen.Count == 0
                && settings.UnixSockets.Count == 0)
            {
                settings = settings with { Listen = [ServeSettingsHelper.ParseListenAddress(@"http://127.0.0.1:0")] };
            }

            WebApplication app = JobServer.Build(settings, jobRunner, warmUp);
            var server = new RunningServer(app);

            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments(JobServer.ApiPath))
                {
                    Interlocked.Increment(ref server.m_ApiRequests);
                }

                await next(context);
            });

            await app.StartAsync();

            // A Unix domain socket has no address of its own - the web server names one unix: and its path - so the
            // handler connects to it. A request that is never answered fails its test within a minute, rather than
            // holding the run up.
            string address = settings.Listen.Count == 0
                ? @"http://localhost"
                : app.Urls.First(x => !x.Contains(@"://unix:", StringComparison.Ordinal));
            server.m_Client = new HttpClient(handler ?? new SocketsHttpHandler())
            {
                BaseAddress = new Uri(address),
                Timeout = TimeSpan.FromMinutes(1),
            };

            return server;
        }

        // A request to compile a project, sent as zpp serve takes one: the plan as the named part - project or import - under
        // its file name, and the options, if any, as JSON, declared as such, as zpp sends them.
        public static MultipartFormDataContent CompileContent(
            byte[] plan,
            string filename,
            CompileOptions? options = null,
            string part = @"project")
        {
            var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(plan);
            file.Headers.ContentType = new MediaTypeHeaderValue(@"application/octet-stream");
            content.Add(file, part, filename);

            if (options is not null)
            {
                content.Add(new StringContent(JsonSerializer.Serialize(options, JobJsonHelper.ClientOptions), Encoding.UTF8, @"application/json"), @"options");
            }

            return content;
        }

        // The answer, read as the contract has it - strictly, so that a member it does not name is found.
        public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        {
            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, ProjectEndpoints.JsonOptions)
                ?? throw new InvalidOperationException(json);
        }

        // The answer, which is a problem: of the type RFC 9457 gives it, with the trace id of the request in it and in its
        // Request-Id.
        public static async Task<ProblemResponse> ReadProblemAsync(HttpResponseMessage response)
        {
            response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe(@"application/problem+json");

            ProblemResponse problem = await ReadAsync<ProblemResponse>(response);

            problem.Status.ShouldBe((int)response.StatusCode);
            problem.TraceId.ShouldBe(response.Headers.GetValues(ResponseHeadersMiddleware.RequestIdHeader).ShouldHaveSingleItem());
            return problem;
        }

        public async ValueTask DisposeAsync()
        {
            m_Client?.Dispose();
            await m_App.StopAsync();
            await m_App.DisposeAsync();
        }
    }
}
