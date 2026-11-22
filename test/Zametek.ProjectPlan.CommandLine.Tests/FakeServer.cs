using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Text.Json;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // A server that answers every request to zpp serve's API as the test says - by the number of the request, so that it can
    // be busy at first and answer later - whatever it is sent, and keeps what it was sent, for a test of how zpp talks to a
    // server, and of how it reads what it is answered with. A request for the limits (GET /v1/info) has an answer and a
    // list of its own, so that the number of a request is its number among the jobs.
    internal sealed class FakeServer
        : IAsyncDisposable
    {
        private readonly WebApplication m_App;
        private readonly List<Request> m_Requests = [];
        private readonly List<Request> m_InfoRequests = [];
        private readonly List<string> m_Paths = [];

        private FakeServer(WebApplication app)
        {
            m_App = app;
        }

        // One part of a multipart request.
        public sealed record Part(
            string Name,
            string? FileName,
            string? ContentType,
            byte[] Content);

        // What a request was, as the server saw it.
        public sealed record Request(
            string Path,
            string Query,
            IReadOnlyDictionary<string, string> Headers,
            byte[] Body)
        {
            public async Task<IReadOnlyList<Part>> ReadPartsAsync()
            {
                string boundary = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(Headers[HeaderNames.ContentType]).Boundary).Value
                    ?? throw new InvalidOperationException();
                var reader = new MultipartReader(boundary, new MemoryStream(Body));
                var parts = new List<Part>();

                while (await reader.ReadNextSectionAsync() is MultipartSection section)
                {
                    ContentDispositionHeaderValue disposition = section.GetContentDispositionHeader() ?? throw new InvalidOperationException();
                    using var content = new MemoryStream();
                    await section.Body.CopyToAsync(content);

                    parts.Add(new Part(
                        HeaderUtilities.RemoveQuotes(disposition.Name).Value ?? string.Empty,
                        HeaderUtilities.RemoveQuotes(disposition.FileName).Value,
                        section.ContentType,
                        content.ToArray()));
                }

                return parts;
            }
        }

        // What the server answers a request with, and how long it waits before it answers.
        public sealed record Answer(
            int Status = StatusCodes.Status200OK,
            string Body = @"",
            string ContentType = @"application/json",
            IReadOnlyDictionary<string, string>? Headers = null,
            TimeSpan Delay = default);

        // The limits the server gives when a test does not say: 4 jobs, 8 in the queue, 2 minutes for a job and 1 minute
        // for a compilation.
        public static Answer DefaultInfo { get; } = InfoOf(4, 8, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1));

        // What a zpp serve with these limits says about itself.
        public static Answer InfoOf(
            int maxJobs,
            int maxQueue,
            TimeSpan jobTimeout,
            TimeSpan maxCompileTimeout)
        {
            var limits = new LimitsResponse(maxJobs, maxQueue, 50, 5000, 5000, 100_000, jobTimeout, maxCompileTimeout);
            return new Answer(Body: JsonSerializer.Serialize(new InfoResponse(@"0.0.0", @"en-GB", @"Etc/UTC", limits), JobJsonHelper.ServerOptions));
        }

        public string Address => m_App.Urls.First();

        public IReadOnlyList<Request> Requests
        {
            get
            {
                lock (m_Requests)
                {
                    return [.. m_Requests];
                }
            }
        }

        // The requests for the limits.
        public IReadOnlyList<Request> InfoRequests
        {
            get
            {
                lock (m_Requests)
                {
                    return [.. m_InfoRequests];
                }
            }
        }

        // The path of each request, in the order in which the requests came.
        public IReadOnlyList<string> Paths
        {
            get
            {
                lock (m_Requests)
                {
                    return [.. m_Paths];
                }
            }
        }

        public static async Task<FakeServer> StartAsync(
            Func<int, Answer> answerFor,
            Func<Answer>? infoAnswer = null)
        {
            WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
            builder.WebHost.UseKestrelCore();
            builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
            builder.Services.AddRoutingCore();

            WebApplication app = builder.Build();
            var server = new FakeServer(app);

            static async Task<Request> ReadAsync(HttpContext context)
            {
                using var body = new MemoryStream();
                await context.Request.Body.CopyToAsync(body);

                return new Request(
                    context.Request.Path.Value ?? string.Empty,
                    context.Request.QueryString.Value ?? string.Empty,
                    context.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase),
                    body.ToArray());
            }

            async Task AnswerAsync(HttpContext context)
            {
                Request request = await ReadAsync(context);

                int number;
                lock (server.m_Requests)
                {
                    server.m_Requests.Add(request);
                    server.m_Paths.Add(request.Path);
                    number = server.m_Requests.Count;
                }

                await WriteAsync(context, answerFor(number));
            }

            async Task AnswerInfoAsync(HttpContext context)
            {
                Request request = await ReadAsync(context);

                lock (server.m_Requests)
                {
                    server.m_InfoRequests.Add(request);
                    server.m_Paths.Add(request.Path);
                }

                await WriteAsync(context, infoAnswer?.Invoke() ?? DefaultInfo);
            }

            static async Task WriteAsync(HttpContext context, Answer answer)
            {
                // A client that goes away ends the wait.
                if (answer.Delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(answer.Delay, context.RequestAborted);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }

                context.Response.StatusCode = answer.Status;

                foreach ((string name, string value) in answer.Headers ?? new Dictionary<string, string>())
                {
                    context.Response.Headers[name] = value;
                }

                if (answer.Body.Length > 0)
                {
                    context.Response.ContentType = answer.ContentType;
                    await context.Response.WriteAsync(answer.Body);
                }
            }

            app.MapPost(@"/v1/projects/compile", (RequestDelegate)AnswerAsync);
            app.MapPost(@"/v1/projects/scenarios", (RequestDelegate)AnswerAsync);
            app.MapGet(@"/v1/info", (RequestDelegate)AnswerInfoAsync);

            await app.StartAsync();
            return server;
        }

        public async ValueTask DisposeAsync()
        {
            await m_App.DisposeAsync();
        }
    }
}
