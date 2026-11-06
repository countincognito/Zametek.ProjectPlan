using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using System.Net;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // A server that answers every request to zpp serve's API as the test says - by the number of the request, so that it can
    // be busy at first and answer later - whatever it is sent, and keeps what it was sent, for a test of how zpp talks to a
    // server, and of how it reads what it is answered with.
    internal sealed class FakeServer
        : IAsyncDisposable
    {
        private readonly WebApplication m_App;
        private readonly List<Request> m_Requests = [];

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

        // What the server answers a request with.
        public sealed record Answer(
            int Status = StatusCodes.Status200OK,
            string Body = @"",
            string ContentType = @"application/json",
            IReadOnlyDictionary<string, string>? Headers = null);

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

        public static async Task<FakeServer> StartAsync(Func<int, Answer> answerFor)
        {
            WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
            builder.WebHost.UseKestrelCore();
            builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
            builder.Services.AddRoutingCore();

            WebApplication app = builder.Build();
            var server = new FakeServer(app);

            async Task AnswerAsync(HttpContext context)
            {
                using var body = new MemoryStream();
                await context.Request.Body.CopyToAsync(body);

                int number;
                lock (server.m_Requests)
                {
                    server.m_Requests.Add(new Request(
                        context.Request.Path.Value ?? string.Empty,
                        context.Request.QueryString.Value ?? string.Empty,
                        context.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase),
                        body.ToArray()));
                    number = server.m_Requests.Count;
                }

                Answer answer = answerFor(number);
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

            await app.StartAsync();
            return server;
        }

        public async ValueTask DisposeAsync()
        {
            await m_App.DisposeAsync();
        }
    }
}
