using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.Engine.ProjectPlan.Tests
{
    /// <summary>
    /// Runs whole jobs from bytes in to bytes out: what each request produces,
    /// in what order, how a job ends when an output fails or the plan does not
    /// compile, and that each job leaves nothing behind for the next.
    /// </summary>
    public class JobRunnerTests
    {
        // A fixed clock, so that two runs of the same job save the same project and
        // export the same workbook: saving stamps the scenario's ModifiedOn, and
        // exporting stamps the workbook.
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
            MemoryJobSink? sink = null,
            CancellationToken cancellationToken = default)
        {
            sink ??= new MemoryJobSink();
            await using FileStream input = File.OpenRead(AssetPath(asset));
            JobResult result = await runner.RunAsync(buildRequest(input), sink, cancellationToken);
            return (result, sink);
        }

        public static TheoryData<JobOutput> EveryOutputButTheLast
        {
            get
            {
                var data = new TheoryData<JobOutput>();
                foreach (JobOutput output in Enum.GetValues<JobOutput>().SkipLast(1))
                {
                    data.Add(output);
                }
                return data;
            }
        }

        // Keeps each time as it was written, offset and all, rather than turning it into a local DateTime.
        private static JObject ParseProject(byte[] project)
        {
            using var reader = new JsonTextReader(new StringReader(Encoding.UTF8.GetString(project)))
            {
                DateParseHandling = DateParseHandling.DateTimeOffset,
            };
            return JObject.Load(reader);
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
            result.CompilationErrors.ShouldBeEmpty();
            result.Metrics.ShouldNotBeNull().NetworkDuration.ShouldBe(5);
            sink.Outputs.ShouldBeEmpty();
            sink.Messages.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_ProjectFile_Then_TheFinishIsGivenAsDaysAndAsADate()
        {
            // Five days from the plan's start, 1 January 2024, in a calendar that works every day. The plan shows its
            // finish as days; the metrics give both, whichever the plan shows.
            await using ServiceProvider services = BuildServices();

            (JobResult result, _) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"two-scenarios.zpp",
                input => new JobRequest { Input = input });

            JobMetrics metrics = result.Metrics.ShouldNotBeNull();
            metrics.ProjectFinish.ShouldBe(@"5");
            metrics.ProjectFinishDays.ShouldBe(5);
            metrics.ProjectFinishDate.ShouldBe(new DateOnly(2024, 1, 6));
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
        public async Task RunAsync_Given_AFileThatIsNotAProject_Then_ThrowsProjectNotReadableInTheReadersWords()
        {
            await using ServiceProvider services = BuildServices();
            var sink = new MemoryJobSink();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(@"This is not a plan."));

            ProjectNotReadableException ex = await Should.ThrowAsync<ProjectNotReadableException>(
                () => services.GetRequiredService<JobRunner>().RunAsync(new JobRequest { Input = input, SaveProject = true }, sink));

            // What zpp has always printed for such a file is the reader's own message.
            ex.InnerException.ShouldNotBeNull();
            ex.Message.ShouldNotBeNullOrWhiteSpace();
            ex.Message.ShouldBe(ex.InnerException.Message);
            sink.Outputs.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_AFileThatIsNotAWorkbook_Then_ThrowsProjectNotReadable()
        {
            await using ServiceProvider services = BuildServices();
            var sink = new MemoryJobSink();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(@"This is not a workbook."));

            ProjectNotReadableException ex = await Should.ThrowAsync<ProjectNotReadableException>(
                () => services.GetRequiredService<JobRunner>().RunAsync(
                    new JobRequest { Input = input, ImportFormat = ProjectScenarioImportFormat.Xlsx, SaveProject = true },
                    sink));

            ex.InnerException.ShouldNotBeNull();
            ex.Message.ShouldBe(ex.InnerException.Message);
            sink.Outputs.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_AReaderThatFails_Then_ThrowsProjectNotReadableWithItsMessage()
        {
            var failure = new InvalidOperationException(@"The file is of a version nobody knows.");
            await using ServiceProvider services = BuildServices(x => x.AddScoped<IProjectFileOpen>(_ => new FailingProjectFileOpen(failure)));

            ProjectNotReadableException ex = await Should.ThrowAsync<ProjectNotReadableException>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"two-scenarios.zpp",
                    input => new JobRequest { Input = input }));

            ex.Message.ShouldBe(failure.Message);
            ex.InnerException.ShouldBeSameAs(failure);
        }

        [Fact]
        public async Task RunAsync_Given_AWorkbookReaderThatFails_Then_ThrowsProjectNotReadableWithItsMessage()
        {
            var failure = new InvalidDataException(@"The workbook has no sheets.");
            await using ServiceProvider services = BuildServices(x => x.AddScoped<IProjectScenarioFileImport>(_ => new FailingProjectScenarioFileImport(failure)));

            ProjectNotReadableException ex = await Should.ThrowAsync<ProjectNotReadableException>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"two-scenarios.zpp",
                    input => new JobRequest { Input = input, ImportFormat = ProjectScenarioImportFormat.Xlsx }));

            ex.Message.ShouldBe(failure.Message);
            ex.InnerException.ShouldBeSameAs(failure);
        }

        [Theory]
        [InlineData(typeof(OperationCanceledException))]
        [InlineData(typeof(OutOfMemoryException))]
        public async Task RunAsync_Given_AReaderThatIsCancelledOrRunsOutOfMemory_Then_ThrowsItAsItIs(Type kind)
        {
            // Neither is a file the engine cannot read: a job that was cancelled is cancelled, and one that ran out of
            // memory failed, whatever it was reading.
            var failure = (Exception)Activator.CreateInstance(kind)!;
            await using ServiceProvider services = BuildServices(x => x.AddScoped<IProjectFileOpen>(_ => new FailingProjectFileOpen(failure)));

            Exception thrown = await Should.ThrowAsync<Exception>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"two-scenarios.zpp",
                    input => new JobRequest { Input = input }));

            thrown.ShouldBeAssignableTo(kind);
            thrown.ShouldNotBeOfType<ProjectNotReadableException>();
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
        public async Task RunAsync_Given_BrokenPlan_Then_ListsEachCompilationErrorWithItsCode()
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, _) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"broken-dependency.zpp",
                input => new JobRequest { Input = input });

            JobCompilationError error = result.CompilationErrors.ShouldHaveSingleItem();
            error.Code.ShouldBe(@"P0010");
            error.Message.ShouldContain(@"999 is invalid but referenced by: 1");

            // What zpp prints is made of the same errors.
            result.CompilationOutput.ShouldContain(error.Code);
            result.CompilationOutput.ShouldContain(error.Message);
        }

        [Fact]
        public async Task RunAsync_Given_APlanWithTwoKindsOfError_Then_ListsBothInTheOrderTheyArePrinted()
        {
            // An activity that depends on one that is not there, and two that depend on each other.
            await using ServiceProvider services = BuildServices();
            JObject plan = JObject.Parse(await File.ReadAllTextAsync(AssetPath(@"broken-dependency.zpp")));
            var activities = (JArray)plan[@"Files"]![0]![@"Scenario"]![@"DependentActivities"]!;

            foreach ((int id, int dependency) in new[] { (2, 3), (3, 2) })
            {
                JToken activity = activities[0].DeepClone();
                activity[@"Activity"]![@"Id"] = id;
                activity[@"Activity"]![@"Name"] = $@"Task {id}";
                activity[@"Dependencies"] = new JArray(dependency);
                activities.Add(activity);
            }

            using var input = new MemoryStream(Encoding.UTF8.GetBytes(plan.ToString()));

            JobResult result = await services.GetRequiredService<JobRunner>().RunAsync(new JobRequest { Input = input }, new MemoryJobSink());

            result.CompilationErrors.Select(x => x.Code).ShouldBe([@"P0010", @"P0020"]);
            result.CompilationErrors[0].Message.ShouldContain(@"999 is invalid but referenced by: 1");
            result.CompilationErrors[1].Message.ShouldContain(@"3 -> 2");

            foreach (JobCompilationError error in result.CompilationErrors)
            {
                result.CompilationOutput.ShouldContain(error.Message);
            }
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
        public async Task RunAsync_Given_Now_Then_WhatTheJobStampsIsStampedWithIt()
        {
            // Nowhere near the host's clock, so neither could pass for the other.
            var now = new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.FromHours(-5));
            await using ServiceProvider services = BuildServices();

            (_, MemoryJobSink sink) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"two-scenarios.zpp",
                input => new JobRequest { Input = input, Now = now, SaveProject = true, ExportFormat = ProjectScenarioExportFormat.Xlsx });

            // The scenario the job saved was modified then...
            JObject saved = ParseProject(sink[JobOutput.Project]);
            JToken current = saved[@"Nodes"]!.Single(x => x[@"Id"]!.ToString() == saved[@"Current"]!.ToString());
            current[@"ModifiedOn"]!.Value<DateTimeOffset>().ShouldBe(now);

            // ...and the workbook it exported was created then, every part of it written then as the host's clock
            // tells it (a zip holds only the local time, to two seconds).
            using var workbook = new ZipArchive(new MemoryStream(sink[JobOutput.ScenarioExport]), ZipArchiveMode.Read);
            using (Stream core = workbook.GetEntry(@"docProps/core.xml")!.Open())
            {
                XDocument.Load(core).Descendants(XNamespace.Get(@"http://purl.org/dc/terms/") + @"created").Single().Value
                    .ShouldBe(@"2001-02-03T09:05:06Z");
            }
            workbook.Entries.Select(x => x.LastWriteTime.DateTime).Distinct()
                .ShouldBe([TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local).DateTime]);
        }

        [Fact]
        public async Task RunAsync_Given_TheSameNowOnDifferentClocks_Then_TheSameOutputs()
        {
            var now = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.FromHours(1));
            await using ServiceProvider services = BuildServices();
            await using ServiceProvider later = BuildServices(x => x.AddSingleton<TimeProvider>(new FixedTimeProvider(s_Now.AddDays(1))));

            JobRequest Request(Stream input) =>
                new() { Input = input, Now = now, SaveProject = true, ExportFormat = ProjectScenarioExportFormat.Xlsx };
            (_, MemoryJobSink first) = await RunAsync(services.GetRequiredService<JobRunner>(), @"two-scenarios.zpp", Request);
            (_, MemoryJobSink second) = await RunAsync(later.GetRequiredService<JobRunner>(), @"two-scenarios.zpp", Request);

            second[JobOutput.Project].ShouldBe(first[JobOutput.Project]);
            second[JobOutput.ScenarioExport].ShouldBe(first[JobOutput.ScenarioExport]);
        }

        [Fact]
        public async Task RunAsync_Given_ImportAtNow_Then_TheNewProjectWasCreatedThen()
        {
            var now = new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.FromHours(-5));
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();
            (_, MemoryJobSink exported) = await RunAsync(
                runner,
                @"two-scenarios.zpp",
                input => new JobRequest { Input = input, ExportFormat = ProjectScenarioExportFormat.Xlsx });

            var sink = new MemoryJobSink();
            using var workbook = new MemoryStream(exported[JobOutput.ScenarioExport]);
            await runner.RunAsync(
                new JobRequest { Input = workbook, ImportFormat = ProjectScenarioImportFormat.Xlsx, Now = now, SaveProject = true },
                sink);

            JToken node = ParseProject(sink[JobOutput.Project])[@"Nodes"]!.ShouldHaveSingleItem();
            node[@"CreatedOn"]!.Value<DateTimeOffset>().ShouldBe(now);
            node[@"ModifiedOn"]!.Value<DateTimeOffset>().ShouldBe(now);
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
            afterAnotherSink.Outputs.Select(x => x.Output).ShouldBe(aloneSink.Outputs.Select(x => x.Output));

            foreach ((JobOutput output, byte[] content) in afterAnotherSink.Outputs)
            {
                content.ShouldBe(aloneSink[output], output.ToString());
            }
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
        public async Task RunAsync_Given_ACancelledToken_Then_ThrowsBeforeReadingThePlan()
        {
            await using ServiceProvider services = BuildServices();
            var sink = new MemoryJobSink();
            await using FileStream input = File.OpenRead(AssetPath(@"two-scenarios.zpp"));

            await Should.ThrowAsync<OperationCanceledException>(
                () => services.GetRequiredService<JobRunner>().RunAsync(EveryOutput(input), sink, new CancellationToken(canceled: true)));

            input.Position.ShouldBe(0);
            sink.Outputs.ShouldBeEmpty();
            sink.Messages.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_CancellationWhileThePlanIsRead_Then_ItIsReadAndNotCompiled()
        {
            // A job that asks only for the metrics would have them once it compiled, so throwing shows it never did.
            await using ServiceProvider services = BuildServices();
            using var cancellation = new CancellationTokenSource();
            using var input = new CancellingStream(await File.ReadAllBytesAsync(AssetPath(@"two-scenarios.zpp")), cancellation);

            await Should.ThrowAsync<OperationCanceledException>(
                () => services.GetRequiredService<JobRunner>().RunAsync(new JobRequest { Input = input }, new MemoryJobSink(), cancellation.Token));

            input.Position.ShouldBe(input.Length);
        }

        [Fact]
        public async Task RunAsync_Given_CancellationWhileThePlanIsCompiled_Then_NothingIsProduced()
        {
            // The compile under way runs to its end, and the project it would have saved is never started. The job is
            // cancelled at a point only the job reaches: when it builds the compilation output, which it does itself
            // right after it compiles, through its own handle on the output manager. The view models build things of
            // their own accord too, on other threads and as often as the threads allow - one more of them, or one
            // fewer, from one run to the next - so counting what they build cannot say where the job has got to.
            // What is built after the job was cancelled is told from all that by the flow it is built in: the
            // metrics the job still builds are the whole of the rest of its compile.
            var tail = new CompileTail();
            using var cancellation = new CancellationTokenSource();
            await using ServiceProvider services = BuildServices(x =>
            {
                x.AddScoped<IMetricCalculationService>(provider => new TailCountingMetricCalculationService(
                    ActivatorUtilities.CreateInstance<MetricCalculationService>(provider), tail));
                x.AddScoped<IOutputManagerViewModel>(provider => new CancellingOutputManagerViewModel(
                    ActivatorUtilities.CreateInstance<OutputManagerViewModel>(provider), cancellation, tail));
            });
            var sink = new MemoryJobSink();

            await Should.ThrowAsync<OperationCanceledException>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"two-scenarios.zpp",
                    input => new JobRequest { Input = input, SaveProject = true },
                    sink,
                    cancellation.Token));

            sink.Outputs.ShouldBeEmpty();
            tail.MetricsBuilt.ShouldBe((Network: 1, Risk: 1, Financial: 1));
        }

        [Theory]
        [MemberData(nameof(EveryOutputButTheLast))]
        public async Task RunAsync_Given_CancellationWhileAnOutputIsProduced_Then_ItIsTheLastOneProduced(JobOutput cancelledDuring)
        {
            // The output under way is finished and handed over; the next one is never started.
            await using ServiceProvider services = BuildServices();
            using var cancellation = new CancellationTokenSource();
            var sink = new MemoryJobSink
            {
                OnWrite = output =>
                {
                    if (output == cancelledDuring)
                    {
                        cancellation.Cancel();
                    }
                },
            };

            await Should.ThrowAsync<OperationCanceledException>(
                () => RunAsync(services.GetRequiredService<JobRunner>(), @"two-scenarios.zpp", EveryOutput, sink, cancellation.Token));

            sink.Outputs.Select(x => x.Output).ShouldBe(Enum.GetValues<JobOutput>().TakeWhile(x => x != cancelledDuring).Append(cancelledDuring));
            sink.Messages.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_CancellationWhileTheLastOutputIsProduced_Then_TheJobCompletes()
        {
            // Once the last output is under way there is no step left to stop before.
            await using ServiceProvider services = BuildServices();
            using var cancellation = new CancellationTokenSource();
            JobOutput last = Enum.GetValues<JobOutput>().Last();
            var sink = new MemoryJobSink
            {
                OnWrite = output =>
                {
                    if (output == last)
                    {
                        cancellation.Cancel();
                    }
                },
            };

            (JobResult result, _) = await RunAsync(services.GetRequiredService<JobRunner>(), @"two-scenarios.zpp", EveryOutput, sink, cancellation.Token);

            result.Status.ShouldBe(JobStatus.Succeeded);
            sink.Outputs.Select(x => x.Output).ShouldBe(Enum.GetValues<JobOutput>());
        }

        [Fact]
        public async Task RunAsync_Given_TheSinkGivesUpOnAChartBecauseTheJobWasCancelled_Then_TheJobStopsWithoutReportingIt()
        {
            // A chart the sink cannot store is reported, and the job carries on - but not when the job itself is what
            // stopped it.
            await using ServiceProvider services = BuildServices();
            using var cancellation = new CancellationTokenSource();
            var sink = new MemoryJobSink
            {
                OnWrite = output =>
                {
                    if (output == JobOutput.GanttChart)
                    {
                        cancellation.Cancel();
                        cancellation.Token.ThrowIfCancellationRequested();
                    }
                },
            };

            await Should.ThrowAsync<OperationCanceledException>(
                () => RunAsync(services.GetRequiredService<JobRunner>(), @"two-scenarios.zpp", EveryOutput, sink, cancellation.Token));

            sink.Outputs.Select(x => x.Output).ShouldBe(Enum.GetValues<JobOutput>().TakeWhile(x => x != JobOutput.GanttChart));
            sink.Messages.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_TheSinkCancelsAChartOfItsOwnAccord_Then_ItIsReportedAndTheJobCarriesOn()
        {
            // Cancelled, but not by the job: that is a chart the sink could not store, like any other.
            await using ServiceProvider services = BuildServices();
            var sink = new MemoryJobSink
            {
                OnWrite = output =>
                {
                    if (output == JobOutput.GanttChart)
                    {
                        throw new OperationCanceledException();
                    }
                },
            };

            (JobResult result, _) = await RunAsync(services.GetRequiredService<JobRunner>(), @"two-scenarios.zpp", EveryOutput, sink);

            result.Status.ShouldBe(JobStatus.CompletedWithErrors);
            sink.Outputs.Select(x => x.Output).ShouldBe(Enum.GetValues<JobOutput>().Where(x => x != JobOutput.GanttChart));
            sink.Messages.ShouldHaveSingleItem().Kind.ShouldBe(JobMessageKind.Error);
        }

        [Fact]
        public async Task ListScenariosAsync_Given_ACancelledToken_Then_ThrowsBeforeReadingThePlan()
        {
            await using ServiceProvider services = BuildServices();
            await using FileStream input = File.OpenRead(AssetPath(@"two-scenarios.zpp"));

            await Should.ThrowAsync<OperationCanceledException>(
                () => services.GetRequiredService<JobRunner>().ListScenariosAsync(input, new CancellationToken(canceled: true)));

            input.Position.ShouldBe(0);
        }

        [Fact]
        public async Task ListScenariosAsync_Given_AFileThatIsNotAProject_Then_ThrowsProjectNotReadable()
        {
            await using ServiceProvider services = BuildServices();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(@"This is not a plan."));

            ProjectNotReadableException ex = await Should.ThrowAsync<ProjectNotReadableException>(
                () => services.GetRequiredService<JobRunner>().ListScenariosAsync(input));

            ex.InnerException.ShouldNotBeNull();
            ex.Message.ShouldBe(ex.InnerException.Message);
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

        // What the job builds from the moment it is cancelled, kept apart from what else is built meanwhile. Start is
        // called in the flow of the job, and an async-local value flows into whatever that flow goes on to do, and
        // into nothing that was started before: not the work the view models queue when they are made, which may
        // run at any time, on any thread.
        private sealed class CompileTail
        {
            private readonly AsyncLocal<bool> m_IsTail = new();
            private int m_NetworkMetrics;
            private int m_RiskMetrics;
            private int m_FinancialMetrics;

            public (int Network, int Risk, int Financial) MetricsBuilt =>
                (Volatile.Read(ref m_NetworkMetrics), Volatile.Read(ref m_RiskMetrics), Volatile.Read(ref m_FinancialMetrics));

            public void Start() => m_IsTail.Value = true;

            public void NetworkMetricsBuilt() => Count(ref m_NetworkMetrics);

            public void RiskMetricsBuilt() => Count(ref m_RiskMetrics);

            public void FinancialMetricsBuilt() => Count(ref m_FinancialMetrics);

            private void Count(ref int built)
            {
                if (m_IsTail.Value)
                {
                    Interlocked.Increment(ref built);
                }
            }
        }

        // Works out a plan's metrics as the engine does, counting those built in the tail of the job's compile.
        private sealed class TailCountingMetricCalculationService(
            IMetricCalculationService metrics,
            CompileTail tail)
            : IMetricCalculationService
        {
            public NetworkModel BuildNetworkMetrics(
                IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
                bool hasCompilationErrors,
                DateTimeOffset projectStart,
                int? startTime,
                int? finishTime)
            {
                tail.NetworkMetricsBuilt();
                return metrics.BuildNetworkMetrics(graphCompilation, hasCompilationErrors, projectStart, startTime, finishTime);
            }

            public RisksModel BuildRiskMetrics(
                IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
                bool hasCompilationErrors,
                IEnumerable<ActivitySeverityModel> activitySeverities)
            {
                tail.RiskMetricsBuilt();
                return metrics.BuildRiskMetrics(graphCompilation, hasCompilationErrors, activitySeverities);
            }

            public (CostsModel costs, BillingsModel billings, MarginsModel margins, EffortsModel efforts, List<ResourceMetricsModel> resourceMetrics)
                BuildFinancialMetrics(
                ResourceSeriesSetModel resourceSeriesSet,
                bool hasCompilationErrors)
            {
                tail.FinancialMetricsBuilt();
                return metrics.BuildFinancialMetrics(resourceSeriesSet, hasCompilationErrors);
            }
        }

        // The output manager as the job sees it, which cancels the job when the job builds the compilation output: the
        // job does that itself, once, right after it compiles. The output manager's own reactive work builds it
        // through the class, not through this interface, so it never reaches here.
        private sealed class CancellingOutputManagerViewModel(
            IOutputManagerViewModel outputs,
            CancellationTokenSource cancellation,
            CompileTail tail)
            : IOutputManagerViewModel
        {
            public bool IsBusy => outputs.IsBusy;

            public bool HasStaleOutputs => outputs.HasStaleOutputs;

            public bool HasCompilationErrors => outputs.HasCompilationErrors;

            public string CompilationOutput => outputs.CompilationOutput;

            public void BuildCompilationOutput()
            {
                outputs.BuildCompilationOutput();
                cancellation.Cancel();
                tail.Start();
            }

            public void StartSubscriptions() => outputs.StartSubscriptions();

            public void KillSubscriptions() => outputs.KillSubscriptions();

            public void Dispose() => outputs.Dispose();
        }

        // A plan that cancels the job as soon as the job starts to read it.
        private sealed class CancellingStream(byte[] content, CancellationTokenSource cancellation)
            : MemoryStream(content)
        {
            public override int Read(byte[] buffer, int offset, int count)
            {
                cancellation.Cancel();
                return base.Read(buffer, offset, count);
            }

            public override int Read(Span<byte> buffer)
            {
                cancellation.Cancel();
                return base.Read(buffer);
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                cancellation.Cancel();
                return base.ReadAsync(buffer, offset, count, cancellationToken);
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                cancellation.Cancel();
                return base.ReadAsync(buffer, cancellationToken);
            }
        }

        // The reader of projects, failing with whatever it is told to fail with.
        private sealed class FailingProjectFileOpen(Exception exception)
            : IProjectFileOpen
        {
            public Task<ProjectModel> OpenProjectFileAsync(Stream stream) => Task.FromException<ProjectModel>(exception);
        }

        // The reader of workbooks, likewise.
        private sealed class FailingProjectScenarioFileImport(Exception exception)
            : IProjectScenarioFileImport
        {
            public ProjectScenarioImportModel ImportProjectScenarioFile(Stream stream, ProjectScenarioImportFormat format) => throw exception;
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
