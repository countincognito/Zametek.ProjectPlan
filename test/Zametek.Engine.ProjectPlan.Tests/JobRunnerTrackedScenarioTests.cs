using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Shouldly;
using System.Text;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.Engine.ProjectPlan.Tests
{
    /// <summary>
    /// Runs jobs on a project of two tracked scenarios: Alpha, a task of 5 days and the current scenario, and Beta, a task of
    /// 10 days whose stored metrics are stale (they have no network at all, as in a file that a program wrote). A job that
    /// compiles the tracked scenarios gives each its metrics, keeps them in the project, and still loads the scenario that it
    /// would load without them. The scenario chart compiles them also when the request does not ask.
    /// </summary>
    public class JobRunnerTrackedScenarioTests
    {
        private static readonly DateTimeOffset s_Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

        private static readonly Guid s_AlphaId = Guid.Parse(@"8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5");
        private static readonly Guid s_BetaId = Guid.Parse(@"17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4");

        public JobRunnerTrackedScenarioTests()
        {
            ProjectPlanEngine.Initialize();
        }

        private static byte[] Plan()
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, @"Assets", @"stale-tracked.zpp"));
        }

        private static ServiceProvider BuildServices()
        {
            IServiceCollection services = new ServiceCollection()
                .AddProjectPlanEngine()
                .AddSingleton<TimeProvider>(new FixedTimeProvider(s_Now));
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        private static async Task<(JobResult Result, MemoryJobSink Sink)> RunAsync(
            byte[] plan,
            Func<Stream, JobRequest> buildRequest)
        {
            await using ServiceProvider services = BuildServices();
            var sink = new MemoryJobSink();
            using var input = new MemoryStream(plan);
            JobResult result = await services.GetRequiredService<JobRunner>().RunAsync(buildRequest(input), sink);
            return (result, sink);
        }

        // The network metrics that a saved project holds for a scenario.
        private static JToken Network(byte[] project, Guid scenarioId)
        {
            return JObject.Parse(Encoding.UTF8.GetString(project))[@"Files"]!
                .Single(x => Guid.Parse(x[@"NodeId"]!.Value<string>()!) == scenarioId)[@"Scenario"]![@"Metrics"]![@"Network"]!;
        }

        [Fact]
        public async Task RunAsync_Given_CompileTracked_Then_TheResultHasTheMetricsOfEachTrackedScenario()
        {
            (JobResult result, _) = await RunAsync(Plan(), input => new JobRequest { Input = input, CompileTracked = true });

            result.Status.ShouldBe(JobStatus.Succeeded);

            // The job loads the scenario that it loads without the option, and its metrics are those of the job.
            result.Metrics.ShouldNotBeNull().NetworkDuration.ShouldBe(5);
            IReadOnlyList<JobTrackedScenario> tracked = result.TrackedScenarios.ShouldNotBeNull();
            tracked.Select(x => (x.Path, x.Id, x.IsCurrent, x.Metrics?.NetworkDuration)).ShouldBe(
            [
                (@"Alpha", s_AlphaId, true, 5),
                (@"Beta", s_BetaId, false, 10),
            ]);
            tracked[0].Metrics.ShouldBe(result.Metrics);
        }

        [Fact]
        public async Task RunAsync_Given_CompileTracked_Then_EachScenarioHasTheMetricsThatAJobThatLoadsItGives()
        {
            (JobResult beta, _) = await RunAsync(Plan(), input => new JobRequest { Input = input, Scenario = @"Beta" });

            (JobResult result, _) = await RunAsync(Plan(), input => new JobRequest { Input = input, CompileTracked = true });

            result.TrackedScenarios.ShouldNotBeNull().Single(x => x.Id == s_BetaId).Metrics.ShouldBe(beta.Metrics);
        }

        [Fact]
        public async Task RunAsync_Given_CompileTrackedAndAnotherScenario_Then_ThatScenarioIsTheCurrentOneOfTheList()
        {
            (JobResult result, MemoryJobSink sink) = await RunAsync(
                Plan(),
                input => new JobRequest { Input = input, Scenario = @"Beta", CompileTracked = true, SaveProject = true });

            result.Metrics.ShouldNotBeNull().NetworkDuration.ShouldBe(10);
            result.TrackedScenarios.ShouldNotBeNull().Select(x => (x.Id, x.IsCurrent, x.Metrics?.NetworkDuration)).ShouldBe(
            [
                (s_AlphaId, false, 5),
                (s_BetaId, true, 10),
            ]);
            JObject.Parse(Encoding.UTF8.GetString(sink[JobOutput.Project]))[@"Current"]!.Value<string>().ShouldBe(s_BetaId.ToString());
        }

        [Fact]
        public async Task RunAsync_Given_CompileTrackedAndSaveProject_Then_TheProjectHoldsTheNewMetricsAndTheSameCurrentScenario()
        {
            (_, MemoryJobSink sink) = await RunAsync(Plan(), input => new JobRequest { Input = input, CompileTracked = true, SaveProject = true });

            byte[] project = sink[JobOutput.Project];
            Network(project, s_BetaId)[@"Duration"]!.Value<int>().ShouldBe(10);
            Network(project, s_AlphaId)[@"Duration"]!.Value<int>().ShouldBe(5);
            JObject.Parse(Encoding.UTF8.GetString(project))[@"Current"]!.Value<string>().ShouldBe(s_AlphaId.ToString());
        }

        [Fact]
        public async Task RunAsync_Given_NoOption_Then_TheResultHasNoListAndTheOtherScenariosAreAsTheyWere()
        {
            (JobResult result, MemoryJobSink sink) = await RunAsync(Plan(), input => new JobRequest { Input = input, SaveProject = true });

            result.TrackedScenarios.ShouldBeNull();
            Network(sink[JobOutput.Project], s_BetaId)[@"Duration"].ShouldBeNull();
        }

        [Fact]
        public async Task RunAsync_Given_AScenarioChart_Then_TheTrackedScenariosAreCompiledWithoutTheOption()
        {
            (JobResult result, MemoryJobSink sink) = await RunAsync(
                Plan(),
                input => new JobRequest { Input = input, SaveProject = true, ScenarioChart = new ChartOutputRequest(ChartImageFormat.Svg, 400, 300) });

            result.Status.ShouldBe(JobStatus.Succeeded);
            result.TrackedScenarios.ShouldBeNull();

            // The chart plots the metrics of each tracked scenario, and so they are the new ones: the project holds them.
            Network(sink[JobOutput.Project], s_BetaId)[@"Duration"]!.Value<int>().ShouldBe(10);
        }

        [Fact]
        public async Task RunAsync_Given_ATrackedScenarioThatDoesNotCompile_Then_ItHasNoMetricsAndTheJobSucceeds()
        {
            // Task B of Beta depends on an activity that is not there.
            JObject plan = JObject.Parse(Encoding.UTF8.GetString(Plan()));
            JToken beta = plan[@"Files"]!.Single(x => Guid.Parse(x[@"NodeId"]!.Value<string>()!) == s_BetaId);
            beta[@"Scenario"]![@"DependentActivities"]![0]![@"Dependencies"] = new JArray(999);

            (JobResult result, _) = await RunAsync(
                Encoding.UTF8.GetBytes(plan.ToString()),
                input => new JobRequest { Input = input, CompileTracked = true });

            result.Status.ShouldBe(JobStatus.Succeeded);
            result.TrackedScenarios.ShouldNotBeNull().Select(x => (x.Id, x.Metrics?.NetworkDuration)).ShouldBe(
            [
                (s_AlphaId, 5),
                (s_BetaId, (int?)null),
            ]);
            result.TrackedScenarios.Single(x => x.Id == s_BetaId).Metrics.ShouldBeNull();
        }

        [Fact]
        public async Task RunAsync_Given_ThePlanAndCompileTracked_Then_TheSameResultTwice()
        {
            (JobResult first, MemoryJobSink firstSink) = await RunAsync(Plan(), input => new JobRequest { Input = input, CompileTracked = true, SaveProject = true });
            (JobResult second, MemoryJobSink secondSink) = await RunAsync(Plan(), input => new JobRequest { Input = input, CompileTracked = true, SaveProject = true });

            second.ShouldBe(first);
            secondSink[JobOutput.Project].ShouldBe(firstSink[JobOutput.Project]);
        }

        private sealed class FixedTimeProvider(DateTimeOffset now)
            : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
