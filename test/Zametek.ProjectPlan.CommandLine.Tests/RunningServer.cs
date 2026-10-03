using Microsoft.AspNetCore.Builder;
using System.Net.Http.Headers;
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

        // The job, sent as zpp serve takes one: the plan as the named part - input or import - under its file name, and
        // the options, if any, as JSON.
        public static MultipartFormDataContent JobContent(
            byte[] plan,
            string filename,
            JobOptions? options = null,
            string part = @"input")
        {
            var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(plan);
            file.Headers.ContentType = new MediaTypeHeaderValue(@"application/octet-stream");
            content.Add(file, part, filename);

            if (options is not null)
            {
                content.Add(new StringContent(JsonSerializer.Serialize(options, JobEndpoints.JsonOptions)), @"options");
            }

            return content;
        }

        public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        {
            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, JobEndpoints.JsonOptions)
                ?? throw new InvalidOperationException(json);
        }

        public async ValueTask DisposeAsync()
        {
            m_Client?.Dispose();
            await m_App.StopAsync();
            await m_App.DisposeAsync();
        }
    }
}
