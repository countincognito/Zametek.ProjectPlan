using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ProjectPlan.Engine.Tests
{
    /// <summary>
    /// Jobs running at the same time on one runner each produce exactly what they
    /// produce on their own: nothing one job holds - its scenario, its theme - reaches
    /// another, and nothing they share breaks under them. On Windows this is what
    /// catches a chart written as SVG while others draw losing its text (see
    /// ScottPlotImageExporter).
    /// </summary>
    public class ParallelJobTests
    {
        // How many times each job runs among the others.
        private const int c_Rounds = 3;

        // A fixed clock, so that every run of a job saves the same project.
        private static readonly DateTimeOffset s_Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        public ParallelJobTests()
        {
            ProjectPlanEngine.Initialize();
        }

        [Fact]
        public async Task RunAsync_Given_JobsAtTheSameTime_Then_EachProducesWhatItDoesAlone()
        {
            await using ServiceProvider services = new ServiceCollection()
                .AddProjectPlanEngine()
                .AddSingleton<TimeProvider>(new FixedTimeProvider(s_Now))
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            JobRunner runner = services.GetRequiredService<JobRunner>();

            byte[] twoScenarios = await File.ReadAllBytesAsync(AssetPath(@"two-scenarios.zpp"));
            byte[] broken = await File.ReadAllBytesAsync(AssetPath(@"broken-dependency.zpp"));
            byte[] workbook = (await RunAsync(
                runner,
                @"export",
                twoScenarios,
                input => new JobRequest { Input = input, ExportFormat = ProjectScenarioExportFormat.Xlsx })).Sink[JobOutput.ScenarioExport];

            // The jobs differ in each way one could leak into another: scenario,
            // theme, formats and sizes, the input and how it comes in.
            List<(string Name, byte[] Input, Func<Stream, JobRequest> Build)> jobs =
            [
                (@"alpha", twoScenarios, EveryOutput),
                (@"beta-dark", twoScenarios, input => EveryOutput(input) with { Scenario = @"Beta", BaseTheme = BaseTheme.Dark }),
                (@"rasters", twoScenarios, input => new JobRequest
                {
                    Input = input,
                    GanttChart = new ChartOutputRequest(ChartImageFormat.Png, 640, 480),
                    ArrowGraph = new GraphOutputRequest(GraphExportFormat.GraphML),
                    VertexGraph = new GraphOutputRequest(GraphExportFormat.Png),
                    ResourceChart = new ChartOutputRequest(ChartImageFormat.Jpeg, 640, 480),
                    EarnedValueChart = new ChartOutputRequest(ChartImageFormat.Png, 640, 480),
                }),
                // An import mints new ids for its project, so it is not saved here.
                (@"import", workbook, input => EveryOutput(input) with { ImportFormat = ProjectScenarioImportFormat.Xlsx, SaveProject = false }),
                (@"broken", broken, EveryOutput),
            ];

            // Alone: one after another.
            List<JobRun> alone = [];
            foreach ((string name, byte[] input, Func<Stream, JobRequest> build) in jobs)
            {
                alone.Add(await RunAsync(runner, name, input, build));
            }

            // Together: every job several times over, all at once.
            JobRun[] together = await Task.WhenAll(Enumerable.Range(0, c_Rounds)
                .SelectMany(_ => jobs)
                .Select(job => Task.Run(() => RunAsync(runner, job.Name, job.Input, job.Build))));

            foreach (JobRun run in together)
            {
                JobRun expected = alone.Single(x => x.Name == run.Name);

                run.Result.ShouldBe(expected.Result, run.Name);
                run.Sink.Messages.ShouldBe(expected.Sink.Messages, run.Name);
                run.Sink.Outputs.Select(x => x.Output).ShouldBe(expected.Sink.Outputs.Select(x => x.Output), run.Name);

                foreach ((JobOutput output, byte[] content) in run.Sink.Outputs)
                {
                    OutputComparison.Comparable(output, content).ShouldBe(
                        OutputComparison.Comparable(output, expected.Sink[output]),
                        $@"{run.Name} {output}");
                }
            }
        }

        private static string AssetPath(string filename)
        {
            return Path.Combine(AppContext.BaseDirectory, @"Assets", filename);
        }

        private static JobRequest EveryOutput(Stream input)
        {
            return new JobRequest
            {
                Input = input,
                SaveProject = true,
                ExportFormat = ProjectScenarioExportFormat.Xlsx,
                GanttChart = new ChartOutputRequest(ChartImageFormat.Svg, 800, 600),
                ArrowGraph = new GraphOutputRequest(GraphExportFormat.Svg),
                VertexGraph = new GraphOutputRequest(GraphExportFormat.GraphML),
                ResourceChart = new ChartOutputRequest(ChartImageFormat.Png, 800, 600),
                EarnedValueChart = new ChartOutputRequest(ChartImageFormat.Svg, 800, 600),
                ScenarioChart = new ChartOutputRequest(ChartImageFormat.Svg, 800, 600),
            };
        }

        private static async Task<JobRun> RunAsync(
            JobRunner runner,
            string name,
            byte[] input,
            Func<Stream, JobRequest> buildRequest)
        {
            var sink = new MemoryJobSink();
            using var stream = new MemoryStream(input, writable: false);
            JobResult result = await runner.RunAsync(buildRequest(stream), sink);
            return new JobRun(name, result, sink);
        }

        private sealed record JobRun(string Name, JobResult Result, MemoryJobSink Sink);

        private sealed class FixedTimeProvider(DateTimeOffset now)
            : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
