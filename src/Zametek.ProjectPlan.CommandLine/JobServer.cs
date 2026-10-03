using CommandLine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // zpp serve: zpp as a web server, which keeps the engine warm from one job to the next. It runs each job as zpp would
    // have run it, in a scope of its own, and answers with what zpp would have printed, exited with and written (see
    // JobEndpoints for its API). It listens on this machine alone unless it is told otherwise, and then only with an API
    // key; it runs as many jobs at once as its limits allow, keeps as many more waiting as they allow, and turns any
    // more away; and it says it is ready, at /health/ready, once it has warmed up.
    internal static class JobServer
    {
        #region Fields

        // The first argument that runs zpp as a server rather than once.
        public const string Command = @"serve";

        // Where its API is: everything under it needs the API key, when the server has one.
        public const string ApiPath = @"/v1";

        // The command that lists its options.
        private const string c_HelpCommand = @"zpp " + Command + @" --help";

        private const string c_JobsPolicy = @"jobs";
        private const string c_ReadyTag = @"ready";
        private const string c_Localhost = @"localhost";
        private const long c_Megabyte = 1024 * 1024;

        // How long a job turned away is told to wait before it tries again.
        private const int c_RetryAfterSeconds = 5;

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

                ConfigureSerilog(settings.Verbose);

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
            services.AddProblemDetails();
            services.Configure<FormOptions>(x => x.MultipartBodyLengthLimit = maxRequestBodySize);

            // A server that is stopped lets the jobs it is running finish, as long as they do within their time limit.
            services.Configure<HostOptions>(x => x.ShutdownTimeout = TimeSpan.FromSeconds(limits.JobTimeoutSeconds));

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
                    context.HttpContext.Response.Headers.RetryAfter = c_RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                    await Results.Problem(
                        detail: Resource.ProjectPlan.Messages.Message_ServeBusy,
                        statusCode: StatusCodes.Status503ServiceUnavailable).ExecuteAsync(context.HttpContext);
                };
            });

            services.AddHealthChecks().AddCheck<WarmUpHealthCheck>(@"warm-up", tags: [c_ReadyTag]);

            services.AddSingleton(jobRunner);
            services.AddSingleton(limits);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<JobEndpoints>();
            services.AddSingleton<WarmUpService>();

            if (warmUp)
            {
                services.AddHostedService(x => x.GetRequiredService<WarmUpService>());
            }

            WebApplication app = builder.Build();

            app.UseExceptionHandler();
            app.UseStatusCodePages();

            // Before the limiter, so that a request without the key never takes a job's place.
            if (settings.ApiKey is string apiKey)
            {
                app.UseMiddleware<ApiKeyMiddleware>(apiKey);
            }

            app.UseRateLimiter();

            RouteGroupBuilder api = app.MapGroup(ApiPath);
            api.MapPost(@"/jobs", (RequestDelegate)(x => x.RequestServices.GetRequiredService<JobEndpoints>().RunJobAsync(x)))
                .RequireRateLimiting(c_JobsPolicy);
            api.MapPost(@"/scenarios", (RequestDelegate)(x => x.RequestServices.GetRequiredService<JobEndpoints>().ListScenariosAsync(x)))
                .RequireRateLimiting(c_JobsPolicy);
            api.MapGet(@"/info", (RequestDelegate)(x => x.RequestServices.GetRequiredService<JobEndpoints>().GetInfoAsync(x)));

            // Live as soon as it listens; ready once it has warmed up.
            app.MapHealthChecks(@"/health/live", new HealthCheckOptions { Predicate = _ => false });
            app.MapHealthChecks(@"/health/ready", new HealthCheckOptions { Predicate = x => x.Tags.Contains(c_ReadyTag) });

            // A socket is the server's access control, so only its owner may connect to it. Windows gives a socket the
            // access its directory gives.
            if (settings.UnixSockets.Count > 0)
            {
                app.Lifetime.ApplicationStarted.Register(() =>
                {
                    if (!OperatingSystem.IsWindows())
                    {
                        foreach (string socket in settings.UnixSockets)
                        {
                            File.SetUnixFileMode(socket, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                        }
                    }
                });
            }

            return app;
        }

        #endregion

        #region Private Members

        private static void ConfigureSerilog(bool verbose)
        {
            // On stderr, as zpp's log is, which leaves stdout to whatever runs the server. The log says what the server
            // does - starting, listening, warming up, and a line for each job - and what goes wrong in its jobs and in
            // the web server. --verbose adds their informational output.
            LoggerConfiguration configuration = new LoggerConfiguration()
                .MinimumLevel.Information()
                // Whenever something asks whether the server is ready while it warms up, the health checks log the
                // answer - not yet - as an error. It is expected, and the warm-up logs for itself whether it failed.
                .MinimumLevel.Override(@"Microsoft.Extensions.Diagnostics.HealthChecks", LogEventLevel.Fatal)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    standardErrorFromLevel: LogEventLevel.Verbose,
                    formatProvider: CultureInfo.InvariantCulture);

            if (!verbose)
            {
                configuration
                    .MinimumLevel.Override(@"Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override(@"Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    .MinimumLevel.Override(@"Zametek", LogEventLevel.Warning)
                    .MinimumLevel.Override(typeof(JobServer).Namespace!, LogEventLevel.Information);
            }

            Log.Logger = configuration.CreateLogger();
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
                        listen.UseHttps(settings.Certificate ?? throw new InvalidOperationException());
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
