using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.Engine.ProjectPlan.Tests
{
    /// <summary>
    /// A job runs the view models without starting them (see IStartSubscriptions), so nothing of theirs runs beside the
    /// job: no reactive pipeline builds an output a second time, on a thread of the pool's choosing, while the job
    /// builds it once.
    /// </summary>
    /// <remarks>
    /// That was not so while a view model started its pipelines in its constructor. The job killed each as it resolved
    /// it, but a delivery already on its way to the thread pool could not be stopped, and so a job built an output twice
    /// whenever the pool won the race - about two jobs in five, for the compile's outputs, and for a chart in up to
    /// half. Which is how a cancellation test came to fail on a build server, and why a job could, in principle, have
    /// had its chart's plot replaced under it. These tests hold the schedulers instead of racing them, so what they say
    /// does not depend on how busy the machine is.
    /// </remarks>
    [Collection(nameof(SchedulerSwapCollection))]
    public class UnstartedViewModelTests
    {
        private static readonly DateTimeOffset s_Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        public UnstartedViewModelTests()
        {
            ProjectPlanEngine.Initialize();
        }

        [Fact]
        public async Task RunAsync_Given_EveryOutput_Then_NoViewModelHandsAnythingToAnotherThread()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();

            // A plan with the filters of its Gantt chart and EV chart saved: a view model that applied them by way of a
            // delivery to another thread would show it, as the job would then lack them.
            byte[] plan = WithSavedChartFilters(await File.ReadAllBytesAsync(AssetPath(@"sample_v0_6_1.zpp")));

            // The job as it runs: the pool and the main thread deliver.
            (JobResult free, MemoryJobSink freeSink) = await RunAsync(runner, plan);
            free.Status.ShouldBe(JobStatus.Succeeded);

            // The same job with both held, so that nothing handed to another thread runs, and what is handed over is
            // counted.
            using var held = new ManualSchedulersScope();
            (JobResult result, MemoryJobSink sink) = await RunAsync(runner, plan);

            held.Pool.PendingCount.ShouldBe(0, "a view model handed work to the thread pool");
            held.Main.PendingCount.ShouldBe(0, "a view model handed work to the main thread");

            result.ShouldBe(free);
            sink.Messages.ShouldBe(freeSink.Messages);
            sink.Outputs.Select(x => x.Output).ShouldBe(freeSink.Outputs.Select(x => x.Output));
            foreach ((JobOutput output, byte[] content) in sink.Outputs)
            {
                content.ShouldBe(freeSink[output], $@"{output}");
            }
        }

        [Fact]
        public async Task RunAsync_Given_ChartFiltersSavedInThePlan_Then_TheChartsApplyThem()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();
            byte[] plain = await File.ReadAllBytesAsync(AssetPath(@"sample_v0_6_1.zpp"));
            byte[] saved = WithSavedChartFilters(plain);

            (_, MemoryJobSink without) = await RunAsync(runner, plain);
            (_, MemoryJobSink with) = await RunAsync(runner, saved);
            (_, MemoryJobSink again) = await RunAsync(runner, saved);

            // The saved connections of the Gantt chart and the saved resources of the EV chart are in the charts the job
            // writes - and in the same charts each time it writes them.
            with[JobOutput.GanttChart].ShouldNotBe(without[JobOutput.GanttChart]);
            with[JobOutput.EarnedValueChart].ShouldNotBe(without[JobOutput.EarnedValueChart]);
            with[JobOutput.GanttChart].ShouldBe(again[JobOutput.GanttChart]);
            with[JobOutput.EarnedValueChart].ShouldBe(again[JobOutput.EarnedValueChart]);
        }

        private static ServiceProvider BuildServices()
        {
            return new ServiceCollection()
                .AddProjectPlanEngine()
                .AddSingleton<TimeProvider>(new FixedTimeProvider(s_Now))
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        private static string AssetPath(string filename)
        {
            return Path.Combine(AppContext.BaseDirectory, @"Assets", filename);
        }

        // The sample plan with some of the activities' connections saved for its Gantt chart to show, and some of its
        // resources for the EV chart: both lists are empty in the file.
        private static byte[] WithSavedChartFilters(byte[] plan)
        {
            string text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(plan).TrimStart('﻿');
            string saved = Regex.Replace(text, @"""GanttChartShowConnections"":\s*\[\s*\]", @"""GanttChartShowConnections"": [10, 11, 12, 13]");
            saved = Regex.Replace(saved, @"""EarnedValueShowResources"":\s*\[\s*\]", @"""EarnedValueShowResources"": [1, 2, 3]");
            saved.ShouldNotBe(text, "the plan has no empty filters to fill in");
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(saved);
        }

        private static async Task<(JobResult Result, MemoryJobSink Sink)> RunAsync(JobRunner runner, byte[] plan)
        {
            var sink = new MemoryJobSink();
            using var stream = new MemoryStream(plan, writable: false);
            JobResult result = await runner.RunAsync(
                new JobRequest
                {
                    Input = stream,
                    SaveProject = true,
                    ExportFormat = ProjectScenarioExportFormat.Xlsx,
                    GanttChart = new ChartOutputRequest(ChartImageFormat.Svg, 800, 600),
                    ArrowGraph = new GraphOutputRequest(GraphExportFormat.Svg),
                    VertexGraph = new GraphOutputRequest(GraphExportFormat.GraphML),
                    ResourceChart = new ChartOutputRequest(ChartImageFormat.Png, 800, 600),
                    EarnedValueChart = new ChartOutputRequest(ChartImageFormat.Svg, 800, 600),
                    ScenarioChart = new ChartOutputRequest(ChartImageFormat.Svg, 800, 600),
                },
                sink);
            return (result, sink);
        }

        private sealed class FixedTimeProvider(DateTimeOffset now)
            : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
