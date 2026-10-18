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
    /// order, that the same settings give the same bytes, where the settings come from (the scenario), what a job does with a
    /// result that the scenario holds, and how a job ends when the simulation cannot run or is cancelled.
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

        private static async Task<(JobResult Result, MemoryJobSink Sink)> RunBytesAsync(
            JobRunner runner,
            byte[] project,
            Func<Stream, JobRequest> buildRequest,
            MemoryJobSink? sink = null)
        {
            sink ??= new MemoryJobSink();
            using var input = new MemoryStream(project);
            JobResult result = await runner.RunAsync(buildRequest(input), sink);
            return (result, sink);
        }

        // The settings a test runs with. The seed is saved, and "New seed each run" is on, which is what the settings of a new
        // scenario are: a job uses the seed that is saved.
        private static SimulationSettingsModel Settings(
            int iterations = 500,
            ulong seed = 11) =>
            new() { Iterations = iterations, Seed = seed, UseRandomSeed = true };

        // A job that runs the simulation, with settings of its own in the place of the plan's.
        private static JobRequest Simulate(
            Stream input,
            SimulationSettingsModel? settings = null,
            SimulationCsvFile files = SimulationCsvFile.None,
            ChartOutputRequest? chart = null)
        {
            return new JobRequest
            {
                Input = input,
                RunSimulation = true,
                SimulationSettings = settings ?? Settings(),
                SimulationCsvFiles = files,
                SimulationChart = chart,
            };
        }

        // The project that a job that ran the simulation saved: it holds the settings and the result.
        private static async Task<byte[]> SaveSimulatedProjectAsync(
            JobRunner runner,
            SimulationSettingsModel? settings = null)
        {
            (_, MemoryJobSink sink) = await RunAsync(
                runner,
                @"estimated.zpp",
                input => Simulate(input, settings) with { SaveProject = true });
            return sink[JobOutput.Project];
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
                input => Simulate(input, new SimulationSettingsModel { Iterations = 500, Seed = 11, TargetFinishTime = 30 }));

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
                input => Simulate(input, new SimulationSettingsModel { Iterations = 500, Seed = 11, Distribution = DurationDistribution.Triangular }));

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
            SimulationSettingsModel settings = new() { Iterations = 2000, Seed = 99, TargetFinishTime = 28 };

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

            (JobResult first, _) = await RunAsync(runner, @"estimated.zpp", input => Simulate(input, Settings(2000, 1)));
            (JobResult second, _) = await RunAsync(runner, @"estimated.zpp", input => Simulate(input, Settings(2000, 2)));

            second.Simulation.ShouldNotBeNull().MeanDays.ShouldNotBe(first.Simulation.ShouldNotBeNull().MeanDays);
        }

        [Fact]
        public async Task RunAsync_Given_ASettingsThatDrawsANewSeedEachRun_Then_TheJobUsesTheSeedThatIsSavedAndLeavesTheSettingsAlone()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();
            SimulationSettingsModel settings = Settings(300, 31);
            settings.UseRandomSeed.ShouldBeTrue();

            // Two runs of a plan that is saved with "New seed each run" on give the same result: the job repeats.
            byte[] saved = await SaveSimulatedProjectAsync(runner, settings);
            (JobResult first, _) = await RunBytesAsync(runner, saved, input => new JobRequest { Input = input, RunSimulation = true });
            (JobResult second, _) = await RunBytesAsync(runner, saved, input => new JobRequest { Input = input, RunSimulation = true });

            first.Simulation.ShouldNotBeNull().Seed.ShouldBe(31UL);
            second.ShouldBe(first);

            // The settings of the project that the job saves are the settings that it had: the seed is not the one of a draw, and
            // "New seed each run" is still on.
            (_, MemoryJobSink sink) = await RunBytesAsync(runner, saved, input => new JobRequest { Input = input, RunSimulation = true, SaveProject = true });
            JToken savedSettings = JObject.Parse(Encoding.UTF8.GetString(sink[JobOutput.Project]))[@"Files"]![0]![@"Scenario"]![@"SimulationSettings"]!;
            savedSettings[@"Seed"]!.ToString().ShouldBe(@"31");
            savedSettings[@"UseRandomSeed"]!.Value<bool>().ShouldBeTrue();
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
                input => Simulate(input, new SimulationSettingsModel { Iterations = 300, Seed = 5, Distribution = distribution, ModifiedBetaShape = 6.0 }));

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
                    input => Simulate(input, new SimulationSettingsModel { Iterations = 4000, Seed = 3, Distribution = distribution, ModifiedBetaShape = shape }));
                return result.Simulation.ShouldNotBeNull().StandardDeviationDays;
            }

            (await Deviation(DurationDistribution.BetaPert, 2.0)).ShouldBe(await Deviation(DurationDistribution.BetaPert, 9.0));
            (await Deviation(DurationDistribution.ModifiedBetaPert, 2.0)).ShouldNotBe(await Deviation(DurationDistribution.ModifiedBetaPert, 9.0));
        }

        [Fact]
        public async Task RunAsync_Given_ARunAndNoSettingsOfTheJob_Then_TheSettingsAreTheScenariosOwn()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();

            // The settings that the project was saved with, and not the defaults of a new scenario.
            byte[] saved = await SaveSimulatedProjectAsync(
                runner,
                new SimulationSettingsModel { Iterations = 321, Seed = 77, Distribution = DurationDistribution.Triangular, TargetFinishTime = 29, UseRandomSeed = false });

            (JobResult result, _) = await RunBytesAsync(runner, saved, input => new JobRequest { Input = input, RunSimulation = true });

            JobSimulationMetrics simulation = result.Simulation.ShouldNotBeNull();
            simulation.Iterations.ShouldBe(321);
            simulation.Seed.ShouldBe(77UL);
            simulation.Distribution.ShouldBe(DurationDistribution.Triangular);
            simulation.TargetFinishDays.ShouldBe(29);
        }

        [Fact]
        public async Task RunAsync_Given_AScenarioThatHasNoSettings_Then_ARunUsesTheDefaultsOfANewScenario()
        {
            await using ServiceProvider services = BuildServices();

            // estimated.zpp is of a version that has none: it has the settings of a new scenario.
            (JobResult result, _) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"estimated.zpp",
                input => new JobRequest { Input = input, RunSimulation = true });

            JobSimulationMetrics simulation = result.Simulation.ShouldNotBeNull();
            simulation.Iterations.ShouldBe(new SimulationSettingsModel().Iterations);
            simulation.Seed.ShouldBe(new SimulationSettingsModel().Seed);
            simulation.Distribution.ShouldBe(DurationDistribution.BetaPert);
        }

        [Fact]
        public async Task RunAsync_Given_ARun_Then_TheProjectThatTheJobSavesHoldsTheSettingsAndTheResult()
        {
            await using ServiceProvider services = BuildServices();

            byte[] saved = await SaveSimulatedProjectAsync(
                services.GetRequiredService<JobRunner>(),
                new SimulationSettingsModel { Iterations = 250, Seed = 5, Distribution = DurationDistribution.Uniform, TargetFinishTime = 30, UseRandomSeed = false });

            JToken scenario = JObject.Parse(Encoding.UTF8.GetString(saved))[@"Files"]![0]![@"Scenario"]!;
            scenario[@"SimulationSettings"]![@"Iterations"]!.Value<int>().ShouldBe(250);
            scenario[@"SimulationSettings"]![@"TargetFinishTime"]!.Value<int>().ShouldBe(30);
            scenario[@"SimulationSummary"]![@"IterationCount"]!.Value<int>().ShouldBe(250);
            scenario[@"SimulationSummary"]![@"ValidIterationCount"]!.Value<int>().ShouldBe(250);
            ((JArray)scenario[@"SimulationSummary"]![@"Bins"]!).ShouldNotBeEmpty();
            ((JArray)scenario[@"SimulationSummary"]![@"Paths"]!).ShouldNotBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_AProjectThatHoldsAResultAndNoRun_Then_TheStatisticsAndTheFilesAreThoseOfTheResult()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();
            SimulationSettingsModel settings = new() { Iterations = 400, Seed = 21, TargetFinishTime = 30, UseRandomSeed = false };

            (JobResult run, MemoryJobSink runSink) = await RunAsync(
                runner,
                @"estimated.zpp",
                input => Simulate(input, settings, SimulationCsvFile.All, new ChartOutputRequest(ChartImageFormat.Svg, 800, 600)) with { SaveProject = true });
            byte[] saved = runSink[JobOutput.Project];

            // A job that does not run the simulation draws what the project held.
            (JobResult later, MemoryJobSink laterSink) = await RunBytesAsync(
                runner,
                saved,
                input => new JobRequest
                {
                    Input = input,
                    SimulationCsvFiles = SimulationCsvFile.All,
                    SimulationChart = new ChartOutputRequest(ChartImageFormat.Svg, 800, 600),
                });

            later.Status.ShouldBe(JobStatus.Succeeded);
            later.Simulation.ShouldBe(run.Simulation);

            // The iterations of a run are not in a project file, and so there is no file of them.
            laterSink.Outputs.Select(x => x.Output).ShouldBe(s_SimulationOutputs.Where(x => x != JobOutput.SimulationIterations));
            laterSink.Messages.ShouldBeEmpty();

            foreach ((JobOutput output, byte[] content) in laterSink.Outputs)
            {
                content.ShouldBe(runSink[output], output.ToString());
            }
        }

        [Fact]
        public async Task RunAsync_Given_AProjectThatHoldsAResultAndNoRun_Then_TheProjectItSavesIsTheResultItHeld()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();
            byte[] saved = await SaveSimulatedProjectAsync(runner, Settings(300, 3));

            (_, MemoryJobSink sink) = await RunBytesAsync(runner, saved, input => new JobRequest { Input = input, SaveProject = true });

            JToken scenario = JObject.Parse(Encoding.UTF8.GetString(sink[JobOutput.Project]))[@"Files"]![0]![@"Scenario"]!;
            scenario[@"SimulationSummary"]![@"IterationCount"]!.Value<int>().ShouldBe(300);
        }

        [Fact]
        public async Task RunAsync_Given_SettingsThatAreNotThoseOfTheResultAndNoRun_Then_TheMetricsHaveTheValuesThatMadeTheResult()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();
            byte[] saved = await SaveSimulatedProjectAsync(
                runner,
                new SimulationSettingsModel { Iterations = 300, Seed = 31, Distribution = DurationDistribution.Triangular, UseRandomSeed = false });

            // The settings of the project were changed after the run, and the job does not run again: the statistics are those of
            // the result, and they name the seed and the distribution that made it.
            (JobResult result, _) = await RunBytesAsync(
                runner,
                saved,
                input => new JobRequest
                {
                    Input = input,
                    SimulationSettings = new SimulationSettingsModel
                    {
                        Iterations = 300,
                        Seed = 99,
                        Distribution = DurationDistribution.ModifiedBetaPert,
                        ModifiedBetaShape = 8.0,
                        UseRandomSeed = false,
                    },
                });

            JobSimulationMetrics simulation = result.Simulation.ShouldNotBeNull();
            simulation.Seed.ShouldBe(31UL);
            simulation.Distribution.ShouldBe(DurationDistribution.Triangular);
            simulation.ModifiedBetaShape.ShouldBeNull();
            simulation.Statistics[0].ShouldBe(new SimulationStatisticModel { Name = Resource.ProjectPlan.Labels.Label_SimulationSeed, Value = @"31" });
            simulation.Statistics[1].Value.ShouldBe(StringConverters.DurationDistributionValue(DurationDistribution.Triangular));
            simulation.Statistics.Select(x => x.Name).ShouldNotContain(Resource.ProjectPlan.Labels.Label_SimulationShape);
        }

        [Fact]
        public async Task RunAsync_Given_AProjectThatHoldsAResultAndARun_Then_TheRunReplacesTheResult()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();
            byte[] saved = await SaveSimulatedProjectAsync(runner, Settings(300, 3));

            (JobResult result, MemoryJobSink sink) = await RunBytesAsync(
                runner,
                saved,
                input => new JobRequest { Input = input, RunSimulation = true, SimulationSettings = Settings(450, 9), SaveProject = true });

            result.Simulation.ShouldNotBeNull().Iterations.ShouldBe(450);
            JToken scenario = JObject.Parse(Encoding.UTF8.GetString(sink[JobOutput.Project]))[@"Files"]![0]![@"Scenario"]!;
            scenario[@"SimulationSummary"]![@"IterationCount"]!.Value<int>().ShouldBe(450);
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
                input => Simulate(input, new SimulationSettingsModel { Iterations = 400, Seed = 21, TargetFinishTime = 30 }, SimulationCsvFile.All));

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
                input => Simulate(input, Settings(100, 4), SimulationCsvFile.Paths));

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
        public async Task RunAsync_Given_ARunThatFailsAndAProjectThatHeldAResult_Then_TheOldResultIsNotThatOfTheJob()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();
            byte[] saved = await SaveSimulatedProjectAsync(runner, Settings(200, 2));
            JObject plan = JObject.Parse(Encoding.UTF8.GetString(saved));
            ((JArray)plan[@"Files"]![0]![@"Scenario"]![@"DependentActivities"]!).Clear();

            (JobResult result, MemoryJobSink sink) = await RunBytesAsync(
                runner,
                Encoding.UTF8.GetBytes(plan.ToString()),
                input => new JobRequest { Input = input, RunSimulation = true, SimulationCsvFiles = SimulationCsvFile.All });

            result.Status.ShouldBe(JobStatus.CompletedWithErrors);
            result.Simulation.ShouldBeNull();
            sink.Outputs.ShouldBeEmpty();
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
        public async Task RunAsync_Given_TheOutputsOfASimulationAndNeitherARunNorAResult_Then_ThrowsBeforeTheCompileAndProducesNothing()
        {
            await using ServiceProvider services = BuildServices();
            var sink = new MemoryJobSink();

            SimulationException ex = await Should.ThrowAsync<SimulationException>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"estimated.zpp",
                    input => new JobRequest { Input = input, SaveProject = true, SimulationCsvFiles = SimulationCsvFile.Paths },
                    sink));

            ex.Failure.ShouldBe(SimulationFailure.NoResult);
            ex.Message.ShouldBe(Resource.ProjectPlan.Messages.Message_SimulationOutputsNeedAResult);
            sink.Outputs.ShouldBeEmpty();

            // The chart is an output of the simulation too.
            await Should.ThrowAsync<SimulationException>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"estimated.zpp",
                    input => new JobRequest { Input = input, SimulationChart = new ChartOutputRequest(ChartImageFormat.Png, 8, 8) }));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(1_000_001)]
        public async Task RunAsync_Given_AnIterationCountOutOfRange_Then_ThrowsBeforeTheCompileAndProducesNothing(int iterations)
        {
            await using ServiceProvider services = BuildServices();
            var sink = new MemoryJobSink();

            SimulationException ex = await Should.ThrowAsync<SimulationException>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"estimated.zpp",
                    input => Simulate(input, new SimulationSettingsModel { Iterations = iterations }) with { SaveProject = true },
                    sink));

            ex.Failure.ShouldBe(SimulationFailure.InvalidSettings);
            sink.Outputs.ShouldBeEmpty();
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public async Task RunAsync_Given_AShapeValueThatIsNotAboveZero_Then_ThrowsOnlyForTheDistributionThatUsesIt(double shape)
        {
            await using ServiceProvider services = BuildServices();

            SimulationException ex = await Should.ThrowAsync<SimulationException>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"estimated.zpp",
                    input => Simulate(input, new SimulationSettingsModel { Distribution = DurationDistribution.ModifiedBetaPert, ModifiedBetaShape = shape })));
            ex.Failure.ShouldBe(SimulationFailure.InvalidSettings);

            // The others do not read it.
            (JobResult result, _) = await RunAsync(
                services.GetRequiredService<JobRunner>(),
                @"estimated.zpp",
                request => Simulate(request, new SimulationSettingsModel { Iterations = 50, Distribution = DurationDistribution.Triangular, ModifiedBetaShape = shape }));
            result.Simulation.ShouldNotBeNull();
        }

        [Fact]
        public async Task RunAsync_Given_ARunWithMoreIterationsThanTheRequestAllows_Then_ThrowsBeforeTheCompileAndProducesNothing()
        {
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();
            byte[] saved = await SaveSimulatedProjectAsync(runner, Settings(500));
            var sink = new MemoryJobSink();

            SimulationException ex = await Should.ThrowAsync<SimulationException>(
                () => RunBytesAsync(
                    runner,
                    saved,
                    input => new JobRequest { Input = input, RunSimulation = true, MaximumSimulationIterations = 499, SaveProject = true },
                    sink));

            ex.Failure.ShouldBe(SimulationFailure.IterationsAboveLimit);
            ex.Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_SimulationIterationsAboveLimit, 500, 499));
            sink.Outputs.ShouldBeEmpty();

            // The limit is the most that a run may have, and so the number itself is allowed.
            (JobResult result, _) = await RunBytesAsync(
                runner,
                saved,
                input => new JobRequest { Input = input, RunSimulation = true, MaximumSimulationIterations = 500 });
            result.Simulation.ShouldNotBeNull().Iterations.ShouldBe(500);
        }

        [Fact]
        public async Task RunAsync_Given_ALimitAndNoRun_Then_TheResultThatTheProjectHeldIsNotLimited()
        {
            // The limit is on what a run costs. A result that is already there costs nothing to give back.
            await using ServiceProvider services = BuildServices();
            JobRunner runner = services.GetRequiredService<JobRunner>();
            byte[] saved = await SaveSimulatedProjectAsync(runner, Settings(500));

            (JobResult result, _) = await RunBytesAsync(
                runner,
                saved,
                input => new JobRequest { Input = input, MaximumSimulationIterations = 10 });

            result.Simulation.ShouldNotBeNull().Iterations.ShouldBe(500);
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
        public async Task RunAsync_Given_CancellationWhileTheProjectIsWritten_Then_NoOutputOfTheSimulationFollows()
        {
            await using ServiceProvider services = BuildServices();
            using var cancellation = new CancellationTokenSource();
            var sink = new MemoryJobSink { OnWrite = _ => cancellation.Cancel() };

            // The run goes before the project is written, and its outputs come after it. A cancel that comes with the project
            // stops the job before the first of them.
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
        public async Task RunAsync_Given_CancellationJustBeforeTheSimulationBegins_Then_ItDoesNotRunAtAll()
        {
            // The window that a cancel was once lost in: after the job has looked at its token, and before the run has made
            // a token of its own. A run that was not stopped would go on for a million iterations, and would have a result.
            SimulationChartManagerViewModel? inner = null;
            using var cancellation = new CancellationTokenSource();
            await using ServiceProvider services = BuildServices(x => x.AddScoped<ISimulationChartManagerViewModel>(provider =>
            {
                inner = ActivatorUtilities.CreateInstance<SimulationChartManagerViewModel>(provider);
                ISimulationChartManagerViewModel proxy = DispatchProxy.Create<ISimulationChartManagerViewModel, CancelsTheJobOnceItRuns>();
                var cancelling = (CancelsTheJobOnceItRuns)proxy;
                cancelling.Inner = inner;
                cancelling.Cancellation = cancellation;
                cancelling.CancelsBeforeTheRun = true;
                return proxy;
            }));
            var sink = new MemoryJobSink();

            await Should.ThrowAsync<OperationCanceledException>(
                () => RunAsync(
                    services.GetRequiredService<JobRunner>(),
                    @"estimated.zpp",
                    input => Simulate(input, new SimulationSettingsModel { Iterations = GraphLimits.MaximumSimulationIterations, Seed = 1 }, SimulationCsvFile.All),
                    sink,
                    cancellation.Token));

            inner.ShouldNotBeNull().StatusText.ShouldBe(Resource.ProjectPlan.Labels.Label_SimulationCancelled);
            inner.Summary.ShouldBeNull();
            inner.IsRunning.ShouldBeFalse();
            sink.Outputs.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunAsync_Given_CancellationWhileTheSimulationRuns_Then_ItStopsBeforeItsLastIterationAndTheJobStopsWithIt()
        {
            SimulationChartManagerViewModel? inner = null;
            using var cancellation = new CancellationTokenSource();
            await using ServiceProvider services = BuildServices(x => x.AddScoped<ISimulationChartManagerViewModel>(provider =>
            {
                inner = ActivatorUtilities.CreateInstance<SimulationChartManagerViewModel>(provider);
                ISimulationChartManagerViewModel proxy = DispatchProxy.Create<ISimulationChartManagerViewModel, CancelsTheJobOnceItRuns>();
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
                    input => Simulate(input, new SimulationSettingsModel { Iterations = GraphLimits.MaximumSimulationIterations, Seed = 1 }, SimulationCsvFile.All, new ChartOutputRequest(ChartImageFormat.Png, 800, 600)) with { SaveProject = true },
                    sink,
                    cancellation.Token));

            // The run was cancelled in flight, and not run to its end: its status says so, and it has no result. The project
            // is not written, because the run goes before it.
            inner.ShouldNotBeNull().StatusText.ShouldBe(Resource.ProjectPlan.Labels.Label_SimulationCancelled);
            inner.Summary.ShouldBeNull();
            sink.Outputs.ShouldBeEmpty();
        }

        private sealed class FixedTimeProvider(DateTimeOffset now)
            : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        // The simulation view model, which cancels the job as soon as its run is going, or before it has begun.
        public class CancelsTheJobOnceItRuns
            : DispatchProxy
        {
            public ISimulationChartManagerViewModel Inner { get; set; } = null!;

            public CancellationTokenSource Cancellation { get; set; } = null!;

            // Whether the job is cancelled before the run is called, as a cancel that comes just before the run starts.
            public bool CancelsBeforeTheRun { get; set; }

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                ArgumentNullException.ThrowIfNull(targetMethod);

                if (targetMethod.Name == nameof(ISimulationChartManagerViewModel.RunSimulationWithSavedSeedAsync))
                {
                    // The run is called with the token of the job, and nothing else cancels it.
                    var token = (CancellationToken)args!.Single()!;

                    if (CancelsBeforeTheRun)
                    {
                        Cancellation.Cancel();
                        return Inner.RunSimulationWithSavedSeedAsync(token);
                    }

                    // The call returns at its first await, with the run going.
                    Task run = Inner.RunSimulationWithSavedSeedAsync(token);
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
