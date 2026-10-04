using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Text.Json;
using Xunit;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp serve's warm-up: the sample plan it carries, the jobs it
    /// runs on it - every output, in raster formats and in vector ones - and
    /// that once they have run, the server is ready.
    /// </summary>
    public class WarmUpServiceTests
        : IClassFixture<EngineFixture>
    {
        private readonly EngineFixture m_Engine;

        public WarmUpServiceTests(EngineFixture engine)
        {
            m_Engine = engine;
        }

        [Fact]
        public void OpenSample_Then_APlan()
        {
            using Stream sample = WarmUpService.OpenSample();
            using JsonDocument plan = JsonDocument.Parse(sample);

            plan.RootElement.GetProperty(@"Version").GetString().ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public async Task Jobs_Then_EveryOutputInARasterFormatThenInAVectorOne()
        {
            HashSet<JobOutput> produced = [];

            foreach (CompileOptions options in WarmUpService.Jobs)
            {
                await using Stream input = WarmUpService.OpenSample();
                var sink = new MemoryJobSink(new BufferedConsole());

                JobResult result = await m_Engine.JobRunner.RunAsync(CompileOptionsHelper.ToJobRequest(options, input, null, new ServeLimits()), sink);

                result.Status.ShouldBe(JobStatus.Succeeded);
                produced.UnionWith(sink.Outputs.Select(x => x.Output));
            }

            produced.ShouldBe(Enum.GetValues<JobOutput>(), ignoreOrder: true);

            CompileOptions raster = WarmUpService.Jobs[0];
            CompileOptions vector = WarmUpService.Jobs[^1];
            raster.Outputs.ShouldNotBeNull().GanttChart.ShouldNotBeNull().Format.ShouldBe(PlotExport.Png);
            raster.Outputs.ShouldNotBeNull().ArrowGraph.ShouldNotBeNull().Format.ShouldBe(GraphExport.Png);
            vector.Outputs.ShouldNotBeNull().GanttChart.ShouldNotBeNull().Format.ShouldBe(PlotExport.Svg);
            vector.Outputs.ShouldNotBeNull().ArrowGraph.ShouldNotBeNull().Format.ShouldBe(GraphExport.Svg);
        }

        [Fact]
        public async Task ExecuteAsync_Then_ReadyOnceTheJobsHaveRun()
        {
            using var warmUp = new WarmUpService(m_Engine.JobRunner, new ServeLimits(), NullLogger<WarmUpService>.Instance);
            warmUp.IsReady.ShouldBeFalse();

            await warmUp.StartAsync(CancellationToken.None);
            await warmUp.ExecuteTask.ShouldNotBeNull();

            warmUp.IsReady.ShouldBeTrue();
        }
    }
}
