using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // Warms zpp serve up before it says it is ready: it runs the sample plan it carries through every output - first in
    // raster formats, then in vector ones - so that the first jobs the server is sent do not pay for loading and
    // compiling what every job uses. Until it has, /health/ready says the server is not ready. If it fails, the server
    // stays not ready, and its log says why.
    internal class WarmUpService
        : BackgroundService
    {
        #region Fields

        // The sample, as the project file embeds it.
        private const string c_SampleResourceName = @"warm-up.zpp";

        private readonly JobRunner m_JobRunner;
        private readonly ServeLimits m_Limits;
        private readonly ILogger<WarmUpService> m_Logger;
        private volatile bool m_IsReady;

        #endregion

        #region Ctors

        public WarmUpService(
            JobRunner jobRunner,
            ServeLimits limits,
            ILogger<WarmUpService> logger)
        {
            ArgumentNullException.ThrowIfNull(jobRunner);
            ArgumentNullException.ThrowIfNull(limits);
            ArgumentNullException.ThrowIfNull(logger);
            m_JobRunner = jobRunner;
            m_Limits = limits;
            m_Logger = logger;
        }

        #endregion

        #region Properties

        public bool IsReady => m_IsReady;

        // The jobs it warms up with: every output, in raster formats, then in vector ones.
        internal static IReadOnlyList<CompileOptions> Jobs { get; } =
        [
            new CompileOptions
            {
                Outputs = new OutputsOptions
                {
                    Project = new ProjectOptions(),
                    ScenarioExport = new ScenarioExportOptions(),
                    GanttChart = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                    ArrowGraph = new GraphOptions { Format = GraphExport.Png },
                    VertexGraph = new GraphOptions { Format = GraphExport.Png },
                    ResourceChart = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                    EarnedValueChart = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                    ScenarioChart = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                },
            },
            new CompileOptions
            {
                MetricsFormat = MetricsExport.Json,
                Outputs = new OutputsOptions
                {
                    GanttChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                    ArrowGraph = new GraphOptions { Format = GraphExport.Svg },
                    VertexGraph = new GraphOptions { Format = GraphExport.Svg },
                    ResourceChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                    EarnedValueChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                    ScenarioChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                },
            },
        ];

        #endregion

        #region Public Members

        // The sample plan it warms up on.
        public static Stream OpenSample()
        {
            return typeof(WarmUpService).Assembly.GetManifestResourceStream(c_SampleResourceName)
                ?? throw new InvalidOperationException($@"{c_SampleResourceName} is not embedded in {typeof(WarmUpService).Assembly.GetName().Name}");
        }

        #endregion

        #region BackgroundService Members

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Off the thread that starts the server, which would otherwise not listen until the jobs had run.
            await Task.Yield();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                foreach (CompileOptions options in Jobs)
                {
                    await using Stream input = OpenSample();
                    var console = new BufferedConsole();

                    JobResult result = await m_JobRunner.RunAsync(
                        CompileOptionsHelper.ToJobRequest(options, input, null, m_Limits),
                        new MemoryJobSink(console),
                        stoppingToken);

                    ExitCode exitCode = await JobConsoleHelper.WriteResultAsync(console, result, options.MetricsFormat);

                    if (exitCode != ExitCode.Success)
                    {
                        m_Logger.LogError("Warming up failed, with exit code {ExitCode}, so the server is not ready: {Error}", (int)exitCode, console.Error);
                        return;
                    }
                }

                m_IsReady = true;
                m_Logger.LogInformation("Warmed up in {ElapsedMilliseconds} ms: ready for jobs", stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The server stopped before it had warmed up.
            }
            catch (Exception ex)
            {
                m_Logger.LogError(ex, "Warming up failed, so the server is not ready");
            }
        }

        #endregion
    }
}
