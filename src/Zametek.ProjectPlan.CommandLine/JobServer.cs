using CommandLine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // zpp serve: zpp as a web server, which keeps the engine warm from one job to the next. It runs each job as zpp would
    // have run it, in a scope of its own, and answers with what zpp would have printed, exited with and written (see
    // ProjectEndpoints for its API). It listens on this machine alone unless it is told otherwise, and then only with an API
    // key; it runs as many jobs at once as its limits allow, keeps as many more waiting as they allow, and turns any
    // more away; and it says it is ready, at /health/ready, once it has warmed up.
    internal static class JobServer
    {
        #region Fields

        // The first argument that runs zpp as a server rather than once.
        public const string Command = @"serve";

        // Where its API is: everything under it needs the API key, when the server has one - but its description, which holds
        // nothing that is not in the repository.
        public const string ApiPath = @"/v1";

        // Where the description of the API is served.
        internal const string DescriptionRoute = @"/openapi";
        internal const string DescriptionPath = ApiPath + DescriptionRoute;

        // The command that lists its options.
        private const string c_HelpCommand = @"zpp " + Command + @" --help";

        // The protocols it takes over https: the oldest that are still sound, and the newest, whatever the system allows.
        internal const SslProtocols TlsProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

        // How a line of its log is written for people: the time, the level, the id of the request - if the line belongs to
        // one - and what it says.
        internal const string TextLogTemplate = @"[{Timestamp:HH:mm:ss} {Level:u3}] {TraceIdPrefix:l}{Message:lj}{NewLine}{Exception}";

        private const string c_JobsPolicy = @"jobs";
        private const string c_JsonMediaType = @"application/json";
        private const string c_MultipartMediaType = @"multipart/form-data";
        private const string c_ReadyTag = @"ready";
        private const string c_Localhost = @"localhost";
        private const long c_Megabyte = 1024 * 1024;

        // How long a request turned away is told to wait before it tries again, in seconds.
        public const int RetryAfterSeconds = 5;

        #endregion

        #region Public Members

        // Runs the server until it is stopped - by Ctrl+C, by the system that started it, or by stoppingToken - and
        // returns the exit code zpp ends with: success when it stopped as asked, a usage error when its options or
        // settings cannot be run with, and a failure when it could not start or stopped of its own accord.
        public static async Task<ExitCode> RunAsync(
            string[] args,
            IJobConsole console,
            CancellationToken stoppingToken = default)
        {
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(console);

            using var parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true;
                with.HelpWriter = null;

                // This needs to be included to prevent the --version option.
                with.AutoVersion = false;

                // So that --listen can be given more than once.
                with.AllowMultiInstance = true;
            });

            try
            {
                // As for zpp, what the parser would let pass is refused before it reads the arguments, which it reads as
                // the check gives them back.
                string[] arguments = ArgumentsHelper.Check<ServeOptions>(args, c_HelpCommand);

                ParserResult<ServeOptions> parserResult = parser.ParseArguments<ServeOptions>(arguments);

                if (parserResult is not Parsed<ServeOptions> parsed)
                {
                    return Program.OnParseErrors(parserResult, parserResult.Errors, ServeOptions.Usage);
                }

                ServeSettings settings = ServeSettingsHelper.Resolve(parsed.Value, ServeSettingsHelper.GetSettingsDirectory(), Program.ReadEnvironment());

                // Before anything writes a number or a date: the engine fixes its date formats the first time it uses
                // them.
                if (settings.Culture is CultureInfo culture)
                {
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = culture;
                }

                ConfigureSerilog(settings.Verbose, settings.LogFormat);

                // Said once, where a person who started the server will see it: nothing in front of this address is there to
                // keep the key and the projects out of sight, but the proxy.
                foreach (ListenAddress address in settings.Listen.Where(ServeSettingsHelper.IsPlainHttpBeyondThisMachine))
                {
                    Log.Warning("Listening on {Url} over plain http, as --behind-tls-proxy says: the proxy in front of it must end TLS, and nothing else may be able to reach it", address.Url);
                }

                // A socket it cannot listen on is known at once, rather than after the engine has started; and one that
                // a server which was killed left behind is cleared away, so that it can be started again.
                foreach (string socket in settings.UnixSockets)
                {
                    if (await UnixSocketFileHelper.PrepareAsync(socket, stoppingToken))
                    {
                        Log.Information("Removed {Socket}, which no server was listening on", socket);
                    }
                }

                // Before the engine's container exists, as for zpp.
                ProjectPlanEngine.Initialize();

                await using ServiceProvider engine = Program.BuildServices(validate: true);
                await using WebApplication app = Build(settings, engine.GetRequiredService<JobRunner>(), warmUp: true);

                await app.RunAsync(stoppingToken);
                return ExitCode.Success;
            }
            catch (UsageException ex)
            {
                await console.WriteErrorLineAsync(ex.Message);
                return ExitCode.UsageError;
            }
            catch (Exception ex)
            {
                return await JobConsoleHelper.WriteFailureAsync(console, ex);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        // The server, ready to start: it listens where the settings say, and runs its jobs on jobRunner. Only a server
        // that warms up ever says it is ready.
        internal static WebApplication Build(
            ServeSettings settings,
            JobRunner jobRunner,
            bool warmUp)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(jobRunner);

            ServeLimits limits = settings.Limits;
            long maxRequestBodySize = limits.MaxUploadMegabytes * c_Megabyte;

            // Empty, so that it reads nothing it is not given: no appsettings.json - watching it for changes is what
            // makes the generic host slow to start on Linux - and nothing from the environment but what
            // ServeSettingsHelper reads.
            WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory,
            });

            builder.WebHost.UseKestrelCore();
            builder.WebHost.ConfigureKestrel(kestrel => ConfigureKestrel(kestrel, settings, maxRequestBodySize));

            IServiceCollection services = builder.Services;

            services.AddSerilog();
            services.AddRoutingCore();

            // What is answered as JSON, and the description of the API, is compressed, for a client that accepts it: over https too,
            // as nothing in it is a secret that the request could have put there. A zip is not compressed again.
            services.AddResponseCompression(compression =>
            {
                compression.EnableForHttps = true;
                compression.MimeTypes = [c_JsonMediaType, ProblemHelper.MediaType, .. ProjectEndpoints.DescriptionMediaTypes];
                compression.Providers.Add<BrotliCompressionProvider>();
                compression.Providers.Add<GzipCompressionProvider>();
            });
            services.Configure<BrotliCompressionProviderOptions>(x => x.Level = CompressionLevel.Fastest);
            services.Configure<GzipCompressionProviderOptions>(x => x.Level = CompressionLevel.Fastest);
            services.Configure<FormOptions>(x => x.MultipartBodyLengthLimit = maxRequestBodySize);

            // A server that is stopped lets the jobs it is running finish, as long as they do within their time limit.
            services.Configure<HostOptions>(x => x.ShutdownTimeout = TimeSpan.FromSeconds(limits.JobTimeoutSeconds));

            // A socket is the server's access control, so only the user running the server may connect to it: it is left
            // to that user as soon as it is bound, before it takes a connection (see UnixSocketFileHelper).
            if (settings.UnixSockets.Count > 0)
            {
                services.Configure<SocketTransportOptions>(x => x.CreateBoundListenSocket = UnixSocketFileHelper.CreateBoundListenSocket);
            }

            services.AddRateLimiter(limiter =>
            {
                limiter.AddConcurrencyLimiter(c_JobsPolicy, jobs =>
                {
                    jobs.PermitLimit = limits.MaxJobs;
                    jobs.QueueLimit = limits.MaxQueue;
                    jobs.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                });

                limiter.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable;
                limiter.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                    await ProblemHelper.ToResult(ProblemHelper.Create(
                        context.HttpContext,
                        ProblemKind.Busy,
                        Resource.ProjectPlan.Messages.Message_ServeBusy)).ExecuteAsync(context.HttpContext);
                };
            });

            services.AddHealthChecks().AddCheck<WarmUpHealthCheck>(@"warm-up", tags: [c_ReadyTag]);

            services.AddSingleton(jobRunner);
            services.AddSingleton(limits);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<ProjectEndpoints>();
            services.AddSingleton<WarmUpService>();

            if (warmUp)
            {
                services.AddHostedService(x => x.GetRequiredService<WarmUpService>());
            }

            WebApplication app = builder.Build();

            // First, so that every response has what none is without - the request's id among it - whatever made it.
            app.UseMiddleware<ResponseHeadersMiddleware>();

            // Before whatever answers, so that every answer is.
            app.UseResponseCompression();

            // What the server did not expect, and what nothing answered - an unknown path, a method a path does not take - is
            // a problem like the rest.
            app.UseExceptionHandler(handler => handler.Run(ProblemHelper.WriteUnexpectedErrorAsync));
            app.UseStatusCodePages(ProblemHelper.WriteStatusAsync);

            // Before the limiter, so that a request without the key never takes a job's place.
            if (settings.ApiKey is string apiKey)
            {
                app.UseMiddleware<ApiKeyMiddleware>(apiKey);
            }

            app.UseRateLimiter();

            RouteGroupBuilder api = app.MapGroup(ApiPath);
            api.MapPost(
                @"/projects/compile",
                x => x.RequestServices.GetRequiredService<ProjectEndpoints>().CompileAsync(x))
                .RequireRateLimiting(c_JobsPolicy);
            api.MapPost(
                @"/projects/scenarios",
                x => x.RequestServices.GetRequiredService<ProjectEndpoints>().ListScenariosAsync(x))
                .RequireRateLimiting(c_JobsPolicy);
            api.MapMethods(
                @"/info",
                [HttpMethods.Get, HttpMethods.Head],
                x => x.RequestServices.GetRequiredService<ProjectEndpoints>().GetInfoAsync(x));
            api.MapMethods(
                DescriptionRoute,
                [HttpMethods.Get, HttpMethods.Head],
                x => x.RequestServices.GetRequiredService<ProjectEndpoints>().GetDescriptionAsync(x));

            // What each takes, for a client that asks: the methods, and what a POST takes.
            api.MapMethods(
                @"/projects/compile",
                [HttpMethods.Options],
                x => ProjectEndpoints.GetOptionsAsync(x, @"POST, OPTIONS", c_MultipartMediaType));
            api.MapMethods(
                @"/projects/scenarios",
                [HttpMethods.Options],
                x => ProjectEndpoints.GetOptionsAsync(x, @"POST, OPTIONS", c_MultipartMediaType));
            api.MapMethods(
                @"/info",
                [HttpMethods.Options],
                x => ProjectEndpoints.GetOptionsAsync(x, @"GET, HEAD, OPTIONS"));
            api.MapMethods(
                DescriptionRoute,
                [HttpMethods.Options],
                x => ProjectEndpoints.GetOptionsAsync(x, @"GET, HEAD, OPTIONS"));

            // Live as soon as it listens; ready once it has warmed up. What a probe is answered is of its moment: nothing the
            // health checks add to say so, which Cache-Control does, as it does for everything else.
            app.MapHealthChecks(
                @"/health/live",
                new HealthCheckOptions { Predicate = _ => false, AllowCachingResponses = true });
            app.MapHealthChecks(
                @"/health/ready",
                new HealthCheckOptions { Predicate = x => x.Tags.Contains(c_ReadyTag), AllowCachingResponses = true });

            return app;
        }

        #endregion

        #region Private Members

        private static void ConfigureSerilog(
            bool verbose,
            LogFormat logFormat)
        {
            Log.Logger = CreateLogger(verbose, logFormat);
        }

        // On stderr, as zpp's log is, which leaves stdout to whatever runs the server. The log says what the server does -
        // starting, listening, warming up, and a line for each request - and what goes wrong in its jobs and in the web
        // server; each line says the id of the request it belongs to, which the response says as Request-Id. --verbose adds
        // their informational output.
        internal static Serilog.Core.Logger CreateLogger(
            bool verbose,
            LogFormat logFormat)
        {
            LoggerConfiguration configuration = new LoggerConfiguration()
                .MinimumLevel.Information()
                // Whenever something asks whether the server is ready while it warms up, the health checks log the
                // answer - not yet - as an error. It is expected, and the warm-up logs for itself whether it failed.
                .MinimumLevel.Override(@"Microsoft.Extensions.Diagnostics.HealthChecks", LogEventLevel.Fatal)
                .Enrich.FromLogContext()
                .Enrich.With<TraceIdEnricher>();

            if (logFormat == LogFormat.Json)
            {
                configuration.WriteTo.Console(new LogJsonFormatter(), standardErrorFromLevel: LogEventLevel.Verbose);
            }
            else
            {
                configuration.WriteTo.Console(
                    outputTemplate: TextLogTemplate,
                    standardErrorFromLevel: LogEventLevel.Verbose,
                    formatProvider: CultureInfo.InvariantCulture);
            }

            if (!verbose)
            {
                configuration
                    .MinimumLevel.Override(@"Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override(@"Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    .MinimumLevel.Override(@"Zametek", LogEventLevel.Warning)
                    .MinimumLevel.Override(typeof(JobServer).Namespace!, LogEventLevel.Information);
            }

            return configuration.CreateLogger();
        }

        // How an https address is served: with the certificate, over the protocols TlsProtocols names.
        internal static HttpsConnectionAdapterOptions CreateHttpsOptions(X509Certificate2 certificate)
        {
            ArgumentNullException.ThrowIfNull(certificate);

            return new HttpsConnectionAdapterOptions
            {
                ServerCertificate = certificate,
                SslProtocols = TlsProtocols,
            };
        }

        private static void ConfigureKestrel(
            KestrelServerOptions kestrel,
            ServeSettings settings,
            long maxRequestBodySize)
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = maxRequestBodySize;

            foreach (ListenAddress address in settings.Listen)
            {
                void Configure(ListenOptions listen)
                {
                    if (address.IsHttps)
                    {
                        listen.UseHttps(CreateHttpsOptions(settings.Certificate ?? throw new InvalidOperationException()));
                    }
                }

                if (string.Equals(address.Host, c_Localhost, StringComparison.OrdinalIgnoreCase))
                {
                    kestrel.ListenLocalhost(address.Port, Configure);
                }
                else if (ServeSettingsHelper.IsAnyAddress(address))
                {
                    kestrel.ListenAnyIP(address.Port, Configure);
                }
                else
                {
                    kestrel.Listen(IPAddress.Parse(address.Host), address.Port, Configure);
                }
            }

            foreach (string socket in settings.UnixSockets)
            {
                kestrel.ListenUnixSocket(socket);
            }
        }

        #endregion
    }
}
