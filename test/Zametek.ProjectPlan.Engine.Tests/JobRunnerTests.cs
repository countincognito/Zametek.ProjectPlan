using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Shouldly;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ProjectPlan.Engine.Tests
{
    /// <summary>
    /// Runs whole jobs from bytes in to bytes out: what each request produces,
    /// in what order, how a job ends when an output fails or the plan does not
    /// compile, and that each job leaves nothing behind for the next.
    /// </summary>
    public class JobRunnerTests
    {
        // A fixed clock, so that two runs of the same job save the same project:
        // saving stamps the scenario's ModifiedOn.
        private static readonly DateTimeOffset s_Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        private static readonly Guid s_AlphaId = Guid.Parse(@"8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5");
        private static readonly Guid s_BetaId = Guid.Parse(@"17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4");

        public JobRunnerTests()
        {
            ProjectPlanEngine.Initialize();
        }

        private static string AssetPath(string filename)
        {
            return Path.Combine(AppContext.BaseDirectory, @"Assets", filename);
        }

        private static ServiceProvider BuildServices(Action<IServiceCollection>? configure = null)
        {
            IServiceCollection services = new ServiceCollection()
                .AddProjectPlanEngine()
                .AddSingleton<TimeProvider>(new FixedTimeProvider(s_Now));
            configure?.Invoke(services);
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        private static async Task<(JobResult Result, MemoryJobSink Sink)> RunAsync(
            JobRunner runner,
            string asset,
            Func<Stream, JobRequest> buildRequest,
            MemoryJobSink? sink = null)
        {
            sink ??= new MemoryJobSink();
            await using FileStream input = File.OpenRead(AssetPath(asset));
            JobResult result = await runner.RunAsync(buildRequest(input), sink);
            return (result, sink);
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

        [Fact]
        public async Task RunAsync_Given_ProjectFile_Then_SucceedsWithTheCurrentScenariosMetrics()
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, MemoryJobSink sink) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"two-scenarios.zpp",
                input => new JobRequest { Input = input });

            result.Status.ShouldBe(JobStatus.Succeeded);
            result.CompilationOutput.ShouldBeEmpty();
            result.Metrics.ShouldNotBeNull().NetworkDuration.ShouldBe(5);
            sink.Outputs.ShouldBeEmpty();
            sink.Messages.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_EveryOutput_Then_ProducesEachOnceInOrder()
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, MemoryJobSink sink) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"two-scenarios.zpp",
                EveryOutput);

            result.Status.ShouldBe(JobStatus.Succeeded);
            sink.Outputs.Select(x => x.Output).ShouldBe(Enum.GetValues<JobOutput>());
            sink.Outputs.ShouldAllBe(x => x.Content.Length > 0);
            sink.Messages.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_ScenarioName_Then_ProcessesThatScenario()
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, MemoryJobSink sink) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"two-scenarios.zpp",
                input => new JobRequest { Input = input, Scenario = @"beta", SaveProject = true });

            result.Status.ShouldBe(JobStatus.Succeeded);
            result.Metrics.ShouldNotBeNull().NetworkDuration.ShouldBe(10);

            // The project saves with the scenario the job loaded as its current one.
            JObject saved = JObject.Parse(Encoding.UTF8.GetString(sink[JobOutput.Project]));
            saved[@"Current"]!.ToString().ShouldBe(s_BetaId.ToString());
        }

        [Fact]
        public async Task RunAsync_Given_UnknownScenario_Then_ThrowsBeforeProducingAnything()
        {
            await using ServiceProvider services = BuildServices();
            var sink = new MemoryJobSink();

            ScenarioSelectionException ex = await Should.ThrowAsync<ScenarioSelectionException>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"two-scenarios.zpp",
                    input => new JobRequest { Input = input, Scenario = @"No Such Scenario", SaveProject = true },
                    sink));

            ex.Failure.ShouldBe(ScenarioSelectionFailure.NoMatch);
            ex.Selector.ShouldBe(@"No Such Scenario");
            sink.Outputs.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_BrokenPlan_Then_ReportsCompilationErrorsAndProducesNothing()
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, MemoryJobSink sink) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"broken-dependency.zpp",
                EveryOutput);

            result.Status.ShouldBe(JobStatus.CompilationErrors);
            result.CompilationOutput.ShouldNotBeNullOrWhiteSpace();
            result.Metrics.ShouldBeNull();
            sink.Outputs.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_ChartTheSinkRefuses_Then_ReportsItAndProducesTheRest()
        {
            // As the desktop reports a chart it cannot save in a dialog and carries
            // on, so the job reports it and produces the outputs after it.
            await using ServiceProvider services = BuildServices();
            var sink = new MemoryJobSink();
            sink.FailingOutputs.Add(JobOutput.GanttChart);

            (JobResult result, _) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"two-scenarios.zpp",
                EveryOutput,
                sink);

            result.Status.ShouldBe(JobStatus.CompletedWithErrors);
            result.Metrics.ShouldNotBeNull();
            sink.Outputs.Select(x => x.Output).ShouldBe(Enum.GetValues<JobOutput>().Where(x => x != JobOutput.GanttChart));
            JobMessage message = sink.Messages.ShouldHaveSingleItem();
            message.Kind.ShouldBe(JobMessageKind.Error);
            message.Title.ShouldBe(Resource.ProjectPlan.Titles.Title_Error);
            message.Message.ShouldBe($@"{JobOutput.GanttChart} refused");
        }

        [Theory]
        [InlineData(JobOutput.Project)]
        [InlineData(JobOutput.ScenarioExport)]
        public async Task RunAsync_Given_ProjectOrExportTheSinkRefuses_Then_ThrowsAndProducesNothingAfter(JobOutput refused)
        {
            await using ServiceProvider services = BuildServices();
            var sink = new MemoryJobSink();
            sink.FailingOutputs.Add(refused);

            await Should.ThrowAsync<IOException>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"two-scenarios.zpp",
                    EveryOutput,
                    sink));

            // Only what came before it in the job's order was produced.
            sink.Outputs.Select(x => x.Output).ShouldBe(Enum.GetValues<JobOutput>().TakeWhile(x => x != refused));
        }

        [Fact]
        public async Task RunAsync_Given_Import_Then_SavesItAsTheBaseScenario()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();

            (_, MemoryJobSink exported) = await RunAsync(
                runner,
                @"two-scenarios.zpp",
                input => new JobRequest { Input = input, ExportFormat = ProjectScenarioExportFormat.Xlsx });

            var sink = new MemoryJobSink();
            using var workbook = new MemoryStream(exported[JobOutput.ScenarioExport]);
            JobResult result = await runner.RunAsync(
                new JobRequest { Input = workbook, ImportFormat = ProjectScenarioImportFormat.Xlsx, SaveProject = true },
                sink);

            result.Status.ShouldBe(JobStatus.Succeeded);
            result.Metrics.ShouldNotBeNull().NetworkDuration.ShouldBe(5);

            // Imported into a new project's Base scenario, as the desktop imports it.
            JObject saved = JObject.Parse(Encoding.UTF8.GetString(sink[JobOutput.Project]));
            JToken node = saved[@"Nodes"]!.ShouldHaveSingleItem();
            node[@"Name"]!.ToString().ShouldBe(Resource.ProjectPlan.Labels.Label_BaseNode);
            saved[@"Current"]!.ToString().ShouldBe(node[@"Id"]!.ToString());
            saved[@"Files"]!.ShouldHaveSingleItem()[@"NodeId"]!.ToString().ShouldBe(node[@"Id"]!.ToString());
        }

        [Fact]
        public async Task RunAsync_Given_Input_Then_LeavesItOpenForTheCaller()
        {
            await using ServiceProvider services = BuildServices();
            await using FileStream input = File.OpenRead(AssetPath(@"two-scenarios.zpp"));

            await services.GetRequiredService<JobRunner>().RunAsync(new JobRequest { Input = input }, new MemoryJobSink());

            input.CanRead.ShouldBeTrue();
        }

        [Fact]
        public async Task RunAsync_Given_EarlierJob_Then_SameResultAsOnAFreshRunner()
        {
            // A job that switched scenario and theme, produced every output and
            // had one of them refused must leave nothing behind that the next job
            // could see.
            await using ServiceProvider used = BuildServices();
            JobRunner usedRunner = used.GetRequiredService<JobRunner>();
            var refusing = new MemoryJobSink();
            refusing.FailingOutputs.Add(JobOutput.GanttChart);
            (JobResult earlier, _) = await RunAsync(
                usedRunner,
                @"two-scenarios.zpp",
                input => EveryOutput(input) with { Scenario = @"Beta", BaseTheme = BaseTheme.Dark },
                refusing);
            earlier.Status.ShouldBe(JobStatus.CompletedWithErrors);

            (JobResult afterAnother, MemoryJobSink afterAnotherSink) = await RunAsync(usedRunner, @"two-scenarios.zpp", EveryOutput);

            await using ServiceProvider fresh = BuildServices();
            (JobResult alone, MemoryJobSink aloneSink) = await RunAsync(fresh.GetRequiredService<JobRunner>(), @"two-scenarios.zpp", EveryOutput);

            afterAnother.ShouldBe(alone);

            // Every output but the scenario export, whose workbook NPOI stamps
            // with the time it was created.
            afterAnotherSink[JobOutput.Project].ShouldBe(aloneSink[JobOutput.Project]);
            WithClipPathsRenumbered(afterAnotherSink[JobOutput.GanttChart]).ShouldBe(WithClipPathsRenumbered(aloneSink[JobOutput.GanttChart]));
            afterAnotherSink[JobOutput.ArrowGraph].ShouldBe(aloneSink[JobOutput.ArrowGraph]);
            afterAnotherSink[JobOutput.VertexGraph].ShouldBe(aloneSink[JobOutput.VertexGraph]);
            afterAnotherSink[JobOutput.ResourceChart].ShouldBe(aloneSink[JobOutput.ResourceChart]);
            WithClipPathsRenumbered(afterAnotherSink[JobOutput.EarnedValueChart]).ShouldBe(WithClipPathsRenumbered(aloneSink[JobOutput.EarnedValueChart]));
            WithClipPathsRenumbered(afterAnotherSink[JobOutput.ScenarioChart]).ShouldBe(WithClipPathsRenumbered(aloneSink[JobOutput.ScenarioChart]));
        }

        // Skia names each clip path in an SVG after a counter that lives as long as
        // the process, so the same chart gets different ids depending on how much
        // was drawn before it, by any job. Numbering them in the order they appear
        // leaves everything else to compare.
        private static string WithClipPathsRenumbered(byte[] svg)
        {
            var numbers = new Dictionary<string, int>();
            return Regex.Replace(
                Encoding.UTF8.GetString(svg),
                @"(?<=id=""|url\(#)cl_[0-9a-f]+",
                match =>
                {
                    if (!numbers.TryGetValue(match.Value, out int number))
                    {
                        number = numbers.Count;
                        numbers.Add(match.Value, number);
                    }
                    return $@"cl_{number}";
                });
        }

        [Fact]
        public async Task RunAsync_Given_TwoJobs_Then_EachRunsInItsOwnScopeDisposedAtItsEnd()
        {
            var probes = new List<ScrollManagerProbe>();
            await using ServiceProvider services = BuildServices(x =>
                x.AddScoped<IDataGridScrollManager>(_ =>
                {
                    var probe = new ScrollManagerProbe();
                    probes.Add(probe);
                    return probe;
                }));
            JobRunner runner = services.GetRequiredService<JobRunner>();

            await RunAsync(runner, @"two-scenarios.zpp", input => new JobRequest { Input = input });
            await RunAsync(runner, @"two-scenarios.zpp", input => new JobRequest { Input = input });

            probes.Count.ShouldBe(2);
            probes.ShouldAllBe(x => x.IsDisposed);
        }

        [Fact]
        public async Task ListScenariosAsync_Given_ProjectFile_Then_ListsItsScenarios()
        {
            await using ServiceProvider services = BuildServices();
            await using FileStream input = File.OpenRead(AssetPath(@"two-scenarios.zpp"));

            IReadOnlyList<ScenarioSummary> scenarios = await services.GetRequiredService<JobRunner>().ListScenariosAsync(input);

            scenarios.ShouldBe(
            [
                new ScenarioSummary(@"Alpha", s_AlphaId, IsTracked: true, IsCurrent: true),
                new ScenarioSummary(@"Beta", s_BetaId, IsTracked: false, IsCurrent: false),
            ]);
            input.CanRead.ShouldBeTrue();
        }

        [Fact]
        public void AddProjectPlanEngine_Then_EveryServiceCanBeBuiltWithValidScopes()
        {
            // Each registration's dependencies are registered, and nothing shared
            // between jobs depends on anything that belongs to one job.
            Should.NotThrow(() =>
            {
                using ServiceProvider services = new ServiceCollection()
                    .AddProjectPlanEngine()
                    .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            });
        }

        private sealed class FixedTimeProvider(DateTimeOffset now)
            : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class ScrollManagerProbe
            : IDataGridScrollManager
        {
            public bool IsDisposed { get; private set; }

            public object? GetScrollItem(string name) => null;

            public void SetScrollItem(string name, object? item)
            {
            }

            public void ClearScrollItems()
            {
            }

            public void Dispose() => IsDisposed = true;
        }
    }
}
