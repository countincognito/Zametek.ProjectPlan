using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Shouldly;
using System.Reflection;
using System.Text;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.Engine.ProjectPlan.Tests
{
    /// <summary>
    /// Runs jobs that simulate the schedule: what the result says of the simulation, which outputs it produces and in what
    /// order, that the same settings give the same bytes, and how a job ends when the simulation cannot run or is
    /// cancelled.
    /// </summary>
    public class JobRunnerSimulationTests
    {
        private static readonly DateTimeOffset s_Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        private static readonly JobOutput[] s_SimulationOutputs =
        [
            JobOutput.SimulationChart,
            JobOutput.SimulationStatistics,
            JobOutput.SimulationHistogram,
            JobOutput.SimulationPaths,
            JobOutput.SimulationActivities,
            JobOutput.SimulationIterations,
        ];

        public JobRunnerSimulationTests()
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

        private static JobRequest Simulate(
            Stream input,
            SimulationRequest? simulation = null,
            SimulationCsvFile files = SimulationCsvFile.None,
            ChartOutputRequest? chart = null)
        {
            return new JobRequest
            {
                Input = input,
                Simulation = simulation ?? new SimulationRequest { Iterations = 500, Seed = 11 },
                SimulationCsvFiles = files,
                SimulationChart = chart,
            };
        }

        // The text of a CSV file, without the byte order mark that leads it.
        private static string[] Lines(byte[] file)
        {
            file.Take(3).ShouldBe([(byte)0xEF, (byte)0xBB, (byte)0xBF]);
            return Encoding.UTF8.GetString(file, 3, file.Length - 3).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        [Fact]
        public async Task RunAsync_Given_ASimulation_Then_TheStatisticsAreInTheResult()
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, MemoryJobSink sink) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"estimated.zpp",
                input => Simulate(input, new SimulationRequest { Iterations = 500, Seed = 11, TargetFinishTime = 30 }));

            result.Status.ShouldBe(JobStatus.Succeeded);
            JobSimulationMetrics simulation = result.Simulation.ShouldNotBeNull();
            sink.Outputs.ShouldBeEmpty();

            simulation.Iterations.ShouldBe(500);
            simulation.ValidIterations.ShouldBe(500);
            simulation.FailedIterations.ShouldBe(0);
            simulation.Seed.ShouldBe(11UL);
            simulation.Distribution.ShouldBe(DurationDistribution.BetaPert);
            simulation.ModifiedBetaShape.ShouldBeNull();

            // The plan as it stands, with the expected duration of each activity, finishes where its compilation says.
            JobMetrics metrics = result.Metrics.ShouldNotBeNull();
            simulation.PlannedFinishDays.ShouldBe(metrics.NetworkDuration.ShouldNotBeNull());
            simulation.PlannedFinishDate.ShouldBe(metrics.ProjectFinishDate.ShouldNotBeNull());

            // The shortest path through the plan is 14 days of its optimistic durations, and the longest 47 of its pessimistic ones.
            simulation.EarliestFinishDays.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(14);
            simulation.LatestFinishDays.ShouldNotBeNull().ShouldBeLessThanOrEqualTo(47);
            simulation.MeanDays.ShouldNotBeNull().ShouldBeInRange(20.0, 32.0);
            simulation.StandardDeviationDays.ShouldNotBeNull().ShouldBeGreaterThan(0.0);

            simulation.Percentiles.Select(x => x.Percentile).ShouldBe([5, 10, 25, 50, 75, 80, 90, 95, 99]);
            simulation.Percentiles.Select(x => x.FinishDays).ShouldBeInOrder();
            simulation.Percentiles.Select(x => x.FinishDate).ShouldBeInOrder();
            simulation.Percentiles.First().FinishDays.ShouldBeGreaterThanOrEqualTo(simulation.EarliestFinishDays.Value);
            simulation.Percentiles.Last().FinishDays.ShouldBeLessThanOrEqualTo(simulation.LatestFinishDays.Value);

            simulation.TargetFinishDays.ShouldBe(30);
            simulation.TargetFinishDate.ShouldNotBeNull();
            simulation.TargetProbability.ShouldNotBeNull().ShouldBeInRange(0.0, 1.0);
        }

        [Fact]
        public async Task RunAsync_Given_ASimulation_Then_TheStatisticsAsTheyAreShownStartWithTheSeedAndTheDistribution()
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, _) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"estimated.zpp",
                input => Simulate(input, new SimulationRequest { Iterations = 500, Seed = 11, Distribution = DurationDistribution.Triangular }));

            IReadOnlyList<SimulationStatisticModel> statistics = result.Simulation.ShouldNotBeNull().Statistics;
            statistics[0].ShouldBe(new SimulationStatisticModel { Name = Resource.ProjectPlan.Labels.Label_SimulationSeed, Value = @"11" });
            statistics[1].Name.ShouldBe(Resource.ProjectPlan.Labels.Label_SimulationDistribution);
            statistics[1].Value.ShouldBe(StringConverters.DurationDistributionValue(DurationDistribution.Triangular));
            statistics.Select(x => x.Name).ShouldContain(Resource.ProjectPlan.Labels.Label_SimulationMean);
            statistics.Select(x => x.Name).ShouldContain(string.Format(Resource.ProjectPlan.Labels.Label_SimulationPercentile, 80));
        }

        [Fact]
        public async Task RunAsync_Given_NoSimulation_Then_TheResultHasNoStatisticsAndNothingIsRunOrProduced()
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, MemoryJobSink sink) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"estimated.zpp",
                input => new JobRequest { Input = input, SaveProject = true });

            result.Simulation.ShouldBeNull();
            sink.Outputs.Select(x => x.Output).ShouldBe([JobOutput.Project]);
        }

        [Fact]
        public async Task RunAsync_Given_TheSameSimulationTwice_Then_TheSameStatisticsAndTheSameFiles()
        {
            await using ServiceProvider services = BuildServices();
            await using ServiceProvider another = BuildServices();
            SimulationRequest settings = new() { Iterations = 2000, Seed = 99, TargetFinishTime = 28 };

            JobRequest Request(Stream input) => Simulate(input, settings, SimulationCsvFile.All, new ChartOutputRequest(ChartImageFormat.Svg, 800, 600));
            (JobResult first, MemoryJobSink firstSink) = await RunAsync(services.GetRequiredService<JobRunner>(), @"estimated.zpp", Request);
            (JobResult second, MemoryJobSink secondSink) = await RunAsync(another.GetRequiredService<JobRunner>(), @"estimated.zpp", Request);

            second.ShouldBe(first);
            secondSink.Outputs.Select(x => x.Output).ShouldBe(s_SimulationOutputs);

            foreach ((JobOutput output, byte[] content) in firstSink.Outputs)
            {
                content.ShouldBe(secondSink[output], output.ToString());
            }
        }

        [Fact]
        public async Task RunAsync_Given_AnotherSeed_Then_AnotherResult()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();

            (JobResult first, _) = await RunAsync(runner, @"estimated.zpp", input => Simulate(input, new SimulationRequest { Iterations = 2000, Seed = 1 }));
            (JobResult second, _) = await RunAsync(runner, @"estimated.zpp", input => Simulate(input, new SimulationRequest { Iterations = 2000, Seed = 2 }));

            second.Simulation.ShouldNotBeNull().MeanDays.ShouldNotBe(first.Simulation.ShouldNotBeNull().MeanDays);
        }

        [Theory]
        [InlineData(DurationDistribution.BetaPert)]
        [InlineData(DurationDistribution.ModifiedBetaPert)]
        [InlineData(DurationDistribution.Triangular)]
        [InlineData(DurationDistribution.Normal)]
        [InlineData(DurationDistribution.Uniform)]
        public async Task RunAsync_Given_EachDistribution_Then_TheSimulationRunsWithIt(DurationDistribution distribution)
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, _) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"estimated.zpp",
                input => Simulate(input, new SimulationRequest { Iterations = 300, Seed = 5, Distribution = distribution, ModifiedBetaShape = 6.0 }));

            JobSimulationMetrics simulation = result.Simulation.ShouldNotBeNull();
            simulation.Distribution.ShouldBe(distribution);
            simulation.ValidIterations.ShouldBe(300);

            // The shape value belongs to the one distribution that has it.
            simulation.ModifiedBetaShape.ShouldBe(distribution == DurationDistribution.ModifiedBetaPert ? 6.0 : null);
        }

        [Fact]
        public async Task RunAsync_Given_TheDistributionsOfTheBetaPert_Then_TheShapeValueChangesTheModifiedOneAndNotTheOther()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();

            async Task<double?> Deviation(DurationDistribution distribution, double shape)
            {
                (JobResult result, _) = await RunAsync(
                    runner,
                    @"estimated.zpp",
                    input => Simulate(input, new SimulationRequest { Iterations = 4000, Seed = 3, Distribution = distribution, ModifiedBetaShape = shape }));
                return result.Simulation.ShouldNotBeNull().StandardDeviationDays;
            }

            (await Deviation(DurationDistribution.BetaPert, 2.0)).ShouldBe(await Deviation(DurationDistribution.BetaPert, 9.0));
            (await Deviation(DurationDistribution.ModifiedBetaPert, 2.0)).ShouldNotBe(await Deviation(DurationDistribution.ModifiedBetaPert, 9.0));
        }

        [Fact]
        public async Task RunAsync_Given_EveryOutputOfTheSimulation_Then_ProducesEachOnceInOrderAfterTheOthers()
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, MemoryJobSink sink) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"estimated.zpp",
                input => Simulate(input, files: SimulationCsvFile.All, chart: new ChartOutputRequest(ChartImageFormat.Png, 800, 600)) with
                {
                    SaveProject = true,
                    GanttChart = new ChartOutputRequest(ChartImageFormat.Png, 800, 600),
                });

            result.Status.ShouldBe(JobStatus.Succeeded);
            sink.Outputs.Select(x => x.Output).ShouldBe([JobOutput.Project, JobOutput.GanttChart, .. s_SimulationOutputs]);
            sink.Outputs.ShouldAllBe(x => x.Content.Length > 0);
            sink.Messages.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_SomeOfTheCsvFiles_Then_ProducesThoseAndNoOthers()
        {
            await using ServiceProvider services = BuildServices();

            (_, MemoryJobSink sink) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"estimated.zpp",
                input => Simulate(input, files: SimulationCsvFile.Iterations | SimulationCsvFile.Statistics));

            sink.Outputs.Select(x => x.Output).ShouldBe([JobOutput.SimulationStatistics, JobOutput.SimulationIterations]);
        }

        [Fact]
        public async Task RunAsync_Given_TheCsvFiles_Then_TheyAreThoseThatThePanelSaves()
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, MemoryJobSink sink) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"estimated.zpp",
                input => Simulate(input, new SimulationRequest { Iterations = 400, Seed = 21, TargetFinishTime = 30 }, SimulationCsvFile.All));

            JobSimulationMetrics simulation = result.Simulation.ShouldNotBeNull();

            string[] statistics = Lines(sink[JobOutput.SimulationStatistics]);
            statistics[0].ShouldBe(@"Statistic,Value");
            statistics.ShouldContain(@"Iterations,400");
            statistics.ShouldContain($@"P80,{simulation.Percentiles.Single(x => x.Percentile == 80).FinishDays}");

            string[] iterations = Lines(sink[JobOutput.SimulationIterations]);
            iterations[0].ShouldBe(@"Iteration,FinishTime,CriticalPaths");
            iterations.Length.ShouldBe(401);

            Lines(sink[JobOutput.SimulationHistogram])[0].ShouldStartWith(@"FirstFinishTime,LastFinishTime,Count,Share,CumulativeShare");
            Lines(sink[JobOutput.SimulationPaths])[0].ShouldBe(@"Rank,Path,HasResourceLinks,Iterations,Share");
            Lines(sink[JobOutput.SimulationActivities])[0].ShouldBe(@"ActivityId,Name,CriticalIterations,CriticalityIndex");

            // Release is on every path.
            Lines(sink[JobOutput.SimulationActivities]).ShouldContain(x => x.StartsWith(@"6,Release,400,1", StringComparison.Ordinal));
        }

        [Fact]
        public async Task RunAsync_Given_APlanWithResources_Then_TheSimulationSchedulesThemAgainInEachIteration()
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, MemoryJobSink sink) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"sample_v0_6_1.zpp",
                input => Simulate(input, new SimulationRequest { Iterations = 100, Seed = 4 }, SimulationCsvFile.Paths));

            result.Status.ShouldBe(JobStatus.Succeeded);
            result.Simulation.ShouldNotBeNull().ValidIterations.ShouldBe(100);
            Lines(sink[JobOutput.SimulationPaths]).Length.ShouldBeGreaterThan(1);
        }

        [Fact]
        public async Task RunAsync_Given_ASinkThatRefusesTheSimulationChart_Then_ReportsItAndProducesTheCsvFiles()
        {
            await using ServiceProvider services = BuildServices();
            var sink = new MemoryJobSink();
            sink.FailingOutputs.Add(JobOutput.SimulationChart);

            (JobResult result, _) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"estimated.zpp",
                input => Simulate(input, files: SimulationCsvFile.All, chart: new ChartOutputRequest(ChartImageFormat.Png, 800, 600)),
                sink);

            result.Status.ShouldBe(JobStatus.CompletedWithErrors);
            result.Simulation.ShouldNotBeNull();
            sink.Outputs.Select(x => x.Output).ShouldBe(s_SimulationOutputs.Skip(1));
            sink.Messages.ShouldHaveSingleItem().Message.ShouldBe($@"{JobOutput.SimulationChart} refused");
        }

        [Fact]
        public async Task RunAsync_Given_APlanWithNoActivities_Then_ReportsItAndProducesNothingOfTheSimulation()
        {
            await using ServiceProvider services = BuildServices();
            JObject plan = JObject.Parse(await File.ReadAllTextAsync(AssetPath(@"two-scenarios.zpp")));
            ((JArray)plan[@"Files"]![0]![@"Scenario"]![@"DependentActivities"]!).Clear();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(plan.ToString()));
            var sink = new MemoryJobSink();

            JobResult result = await services.GetRequiredService<JobRunner>().RunAsync(
                Simulate(input, files: SimulationCsvFile.All, chart: new ChartOutputRequest(ChartImageFormat.Png, 800, 600)),
                sink);

            result.Status.ShouldBe(JobStatus.CompletedWithErrors);
            result.Metrics.ShouldNotBeNull();
            result.Simulation.ShouldBeNull();
            sink.Outputs.ShouldBeEmpty();
            sink.Messages.ShouldHaveSingleItem().Message.ShouldBe(Resource.ProjectPlan.Messages.Message_SimulationNoActivities);
        }

        [Fact]
        public async Task RunAsync_Given_ASimulationAndAPlanThatDoesNotCompile_Then_NothingIsSimulated()
        {
            await using ServiceProvider services = BuildServices();

            (JobResult result, MemoryJobSink sink) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"broken-dependency.zpp",
                input => Simulate(input, files: SimulationCsvFile.All));

            result.Status.ShouldBe(JobStatus.CompilationErrors);
            result.Simulation.ShouldBeNull();
            sink.Outputs.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_TheOutputsOfASimulationAndNoSimulation_Then_ThrowsBeforeReadingThePlan()
        {
            await using ServiceProvider services = BuildServices();
            await using FileStream input = File.OpenRead(AssetPath(@"estimated.zpp"));

            ArgumentException ex = await Should.ThrowAsync<ArgumentException>(
                () => services.GetRequiredService<JobRunner>().RunAsync(
                    new JobRequest { Input = input, SimulationCsvFiles = SimulationCsvFile.Paths },
                    new MemoryJobSink()));
            ex.Message.ShouldContain(Resource.ProjectPlan.Messages.Message_SimulationOutputNeedsSimulation);

            await Should.ThrowAsync<ArgumentException>(
                () => services.GetRequiredService<JobRunner>().RunAsync(
                    new JobRequest { Input = input, SimulationChart = new ChartOutputRequest(ChartImageFormat.Png, 8, 8) },
                    new MemoryJobSink()));

            input.Position.ShouldBe(0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(1_000_001)]
        public async Task RunAsync_Given_AnIterationCountOutOfRange_Then_ThrowsBeforeReadingThePlan(int iterations)
        {
            await using ServiceProvider services = BuildServices();
            await using FileStream input = File.OpenRead(AssetPath(@"estimated.zpp"));

            await Should.ThrowAsync<ArgumentException>(
                () => services.GetRequiredService<JobRunner>().RunAsync(
                    new JobRequest { Input = input, Simulation = new SimulationRequest { Iterations = iterations } },
                    new MemoryJobSink()));

            input.Position.ShouldBe(0);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public async Task RunAsync_Given_AShapeValueThatIsNotAboveZero_Then_ThrowsOnlyForTheDistributionThatUsesIt(double shape)
        {
            await using ServiceProvider services = BuildServices();
            await using FileStream input = File.OpenRead(AssetPath(@"estimated.zpp"));

            await Should.ThrowAsync<ArgumentException>(
                () => services.GetRequiredService<JobRunner>().RunAsync(
                    new JobRequest { Input = input, Simulation = new SimulationRequest { Distribution = DurationDistribution.ModifiedBetaPert, ModifiedBetaShape = shape } },
                    new MemoryJobSink()));

            // The others do not read it.
            (JobResult result, _) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"estimated.zpp",
                request => Simulate(request, new SimulationRequest { Iterations = 50, Distribution = DurationDistribution.Triangular, ModifiedBetaShape = shape }));
            result.Simulation.ShouldNotBeNull();
        }

        [Fact]
        public async Task RunAsync_Given_ACancelledTokenAndASimulation_Then_ThrowsBeforeReadingThePlan()
        {
            await using ServiceProvider services = BuildServices();
            await using FileStream input = File.OpenRead(AssetPath(@"estimated.zpp"));

            await Should.ThrowAsync<OperationCanceledException>(
                () => services.GetRequiredService<JobRunner>().RunAsync(Simulate(input), new MemoryJobSink(), new CancellationToken(canceled: true)));

            input.Position.ShouldBe(0);
        }

        [Fact]
        public async Task RunAsync_Given_CancellationWhileTheOutputBeforeTheSimulationIsProduced_Then_TheSimulationIsNeverStarted()
        {
            await using ServiceProvider services = BuildServices();
            using var cancellation = new CancellationTokenSource();
            var sink = new MemoryJobSink { OnWrite = _ => cancellation.Cancel() };

            await Should.ThrowAsync<OperationCanceledException>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"estimated.zpp",
                    input => Simulate(input, files: SimulationCsvFile.All) with { SaveProject = true },
                    sink,
                    cancellation.Token));

            sink.Outputs.Select(x => x.Output).ShouldBe([JobOutput.Project]);
        }

        [Fact]
        public async Task RunAsync_Given_CancellationWhileTheSimulationRuns_Then_ItStopsBeforeItsLastIterationAndTheJobStopsWithIt()
        {
            ScheduleSimulationManagerViewModel? inner = null;
            using var cancellation = new CancellationTokenSource();
            await using ServiceProvider services = BuildServices(x => x.AddScoped<IScheduleSimulationManagerViewModel>(provider =>
            {
                inner = ActivatorUtilities.CreateInstance<ScheduleSimulationManagerViewModel>(provider);
                IScheduleSimulationManagerViewModel proxy = DispatchProxy.Create<IScheduleSimulationManagerViewModel, CancelsTheJobOnceItRuns>();
                var cancelling = (CancelsTheJobOnceItRuns)proxy;
                cancelling.Inner = inner;
                cancelling.Cancellation = cancellation;
                return proxy;
            }));
            var sink = new MemoryJobSink();

            await Should.ThrowAsync<OperationCanceledException>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"estimated.zpp",
                    input => Simulate(input, new SimulationRequest { Iterations = GraphLimits.MaximumSimulationIterations, Seed = 1 }, SimulationCsvFile.All, new ChartOutputRequest(ChartImageFormat.Png, 800, 600)),
                    sink,
                    cancellation.Token));

            // The run was cancelled in flight, and not run to its end: its status says so, and it has no result.
            inner.ShouldNotBeNull().StatusText.ShouldBe(Resource.ProjectPlan.Labels.Label_SimulationCancelled);
            inner.Summary.ShouldBeNull();
            sink.Outputs.ShouldBeEmpty();
        }

        private sealed class FixedTimeProvider(DateTimeOffset now)
            : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        // The simulation view model, which cancels the job as soon as its run is going: the job cancels the run through
        // the token it registered, and not by calling anything of this class.
        public class CancelsTheJobOnceItRuns
            : DispatchProxy
        {
            public IScheduleSimulationManagerViewModel Inner { get; set; } = null!;

            public CancellationTokenSource Cancellation { get; set; } = null!;

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                ArgumentNullException.ThrowIfNull(targetMethod);

                if (targetMethod.Name == nameof(IScheduleSimulationManagerViewModel.RunSimulationAsync))
                {
                    // The call returns at its first await, with the run going.
                    Task run = Inner.RunSimulationAsync();
                    SpinWait.SpinUntil(() => Inner.IsRunning, TimeSpan.FromSeconds(10)).ShouldBeTrue();
                    Cancellation.Cancel();
                    return run;
                }

                try
                {
                    return targetMethod.Invoke(Inner, args);
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    throw ex.InnerException;
                }
            }
        }
    }
}
