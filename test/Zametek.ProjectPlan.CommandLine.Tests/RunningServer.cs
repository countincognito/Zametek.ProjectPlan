using Microsoft.AspNetCore.Builder;
using System.Net.Http.Headers;
using System.Text.Json;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // A zpp serve started for a test, as JobServer builds it - by default on a port of its own on this machine, chosen
    // when it starts - with a client for it.
    internal sealed class RunningServer
        : IAsyncDisposable
    {
        private readonly WebApplication m_App;

        private RunningServer(
            WebApplication app,
            HttpClient client)
        {
            m_App = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public string Address => m_App.Urls.First();

        public static async Task<RunningServer> StartAsync(
            JobRunner jobRunner,
            ServeSettings? settings = null,
            bool warmUp = false,
            HttpMessageHandler? handler = null)
        {
            settings ??= new ServeSettings();

            if (settings.Listen.Count == 0
                && settings.UnixSocket is null)
            {
                settings = settings with { Listen = [ServeSettingsHelper.ParseListenAddress(@"http://127.0.0.1:0")] };
            }

            WebApplication app = JobServer.Build(settings, jobRunner, warmUp);
            await app.StartAsync();

            // A Unix domain socket has no address of its own: the handler connects to it. A request that is never
            // answered fails its test within a minute, rather than holding the run up.
            string address = settings.Listen.Count == 0 ? @"http://localhost" : app.Urls.First();
            var client = new HttpClient(handler ?? new SocketsHttpHandler())
            {
                BaseAddress = new Uri(address),
                Timeout = TimeSpan.FromMinutes(1),
            };

            return new RunningServer(app, client);
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
            Client.Dispose();
            await m_App.StopAsync();
            await m_App.DisposeAsync();
        }
    }
}
