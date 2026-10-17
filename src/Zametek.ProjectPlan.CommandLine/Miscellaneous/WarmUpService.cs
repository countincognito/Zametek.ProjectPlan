using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ProjectPlan.CommandLine
{
    // Warms zpp serve up before it says it is ready: it runs the sample plan it carries through every output - first in
    // raster formats, then in vector ones - and through a simulation of its schedule, so that the first jobs the server is
    // sent do not pay for loading and compiling what every job uses. Until it has, /health/ready says the server is not
    // ready. If it fails, the server stays not ready, and its log says why.
    internal class WarmUpService
        : BackgroundService
    {
        #region Fields

        // The sample, as the project file embeds it.
        private const string c_SampleResourceName = @"warm-up.zpp";

        // The iterations of the simulation it warms up with: enough to run every chunk path and all the threads, and no more.
        private const int c_WarmUpIterations = 200;

        // A job to warm up with: the options of a request, and the settings of the simulation that it runs. The sample does not
        // hold settings of this kind, and a job that gives its own settings runs with them in the place of the project's.
        internal sealed record WarmUpJob(CompileOptions Options, SimulationSettingsModel Settings);

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
        internal static IReadOnlyList<WarmUpJob> Jobs { get; } =
        [
            new WarmUpJob(
            new CompileOptions
            {
                RunSimulation = true,
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
                    Simulation = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                },
            },
            new SimulationSettingsModel { Iterations = c_WarmUpIterations }),
            new WarmUpJob(
            new CompileOptions
            {
                MetricsFormat = MetricsExport.Json,
                RunSimulation = true,
                Outputs = new OutputsOptions
                {
                    GanttChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                    ArrowGraph = new GraphOptions { Format = GraphExport.Svg },
                    VertexGraph = new GraphOptions { Format = GraphExport.Svg },
                    ResourceChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                    EarnedValueChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                    ScenarioChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                    Simulation = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                },
            },
            new SimulationSettingsModel { Iterations = c_WarmUpIterations, Distribution = DurationDistribution.Triangular }),
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
                foreach (WarmUpJob job in Jobs)
                {
                    await using Stream input = OpenSample();
                    var console = new BufferedConsole();

                    JobResult result = await m_JobRunner.RunAsync(
                        CompileOptionsHelper.ToJobRequest(job.Options, input, null, m_Limits) with { SimulationSettings = job.Settings },
                        new MemoryJobSink(console),
                        stoppingToken);

                    ExitCode exitCode = await JobConsoleHelper.WriteResultAsync(console, result, job.Options.MetricsFormat);

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
