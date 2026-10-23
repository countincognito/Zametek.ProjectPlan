using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A simulation schedules the plan with the builders of the application, which are the builders that a compile
    /// uses (the factory of the application gives them). These tests show that a simulation with those builders
    /// gives what a simulation with the standard engines gives, so that the two agree as long as the application
    /// uses the standard engines.
    /// </summary>
    public class SimulationBuilderTests
    {
        private static async Task<SimulationInputModel> CaptureInputAsync()
        {
            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");
            scenario = scenario with
            {
                DependentActivities =
                [
                    .. scenario.DependentActivities.Select(x => x with
                    {
                        Activity = x.Activity with
                        {
                            MostLikelyDuration = x.Activity.Duration,
                            OptimisticDuration = (int)Math.Round(x.Activity.Duration * 0.75, MidpointRounding.AwayFromZero),
                            PessimisticDuration = (int)Math.Round(x.Activity.Duration * 1.6, MidpointRounding.AwayFromZero),
                        },
                    }),
                ],
            };

            CoreViewModel core = CoreViewModelFixture.Create();
            try
            {
                core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;
                core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");
                core.RunCompile();
                return core.CaptureSimulationInput();
            }
            finally
            {
                core.Dispose();
            }
        }

        [Fact]
        public async Task Run_Given_TheBuilderOfTheApplication_Then_GivesTheResultOfTheStandardEngines()
        {
            SimulationInputModel input = await CaptureInputAsync();
            var settings = new SimulationSettings { Iterations = 300, Seed = 99, MaxDegreeOfParallelism = 2 };

            var standard = new ScheduleSimulator<int, int, int>(input.Activities, input.Resources, input.WorkStreams, input.Estimates);
            var application = new ScheduleSimulator<int, int, int, IDependentActivity>(
                input.Activities.Cast<IDependentActivity>(),
                input.Resources,
                input.WorkStreams,
                input.Estimates,
                new VertexGraphFactory());

            application.CompilationErrors.ShouldBeEmpty();
            application.PlannedFinishTime.ShouldBe(standard.PlannedFinishTime);

            SimulationResult<int> expected = standard.Run(settings);
            SimulationResult<int> actual = application.Run(settings);

            actual.FinishTimes.ShouldBe(expected.FinishTimes);
            actual.CriticalPaths.Count.ShouldBe(expected.CriticalPaths.Count);
            for (int iteration = 0; iteration < expected.IterationCount; iteration++)
            {
                actual.CriticalPathIndexes(iteration).ToArray().ShouldBe(expected.CriticalPathIndexes(iteration).ToArray());
                for (int activity = 0; activity < expected.ActivityIds.Count; activity++)
                {
                    actual.IsCritical(iteration, activity).ShouldBe(expected.IsCritical(iteration, activity), $@"iteration {iteration}, activity {activity}");
                }
            }
        }

        // The planned finish of the simulation is the finish that the compile of the application gave the plan.
        [Fact]
        public async Task PlannedFinishTime_Given_ThePlan_Then_IsTheFinishOfTheCompile()
        {
            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");
            CoreViewModel core = CoreViewModelFixture.Create();
            try
            {
                core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;
                core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");
                core.RunCompile();
                SimulationInputModel input = core.CaptureSimulationInput();

                var application = new ScheduleSimulator<int, int, int, IDependentActivity>(
                    input.Activities.Cast<IDependentActivity>(),
                    input.Resources,
                    input.WorkStreams,
                    input.Estimates,
                    new VertexGraphFactory());

                application.PlannedFinishTime.ShouldBe(core.ProjectFinishDays.ShouldNotBeNull());
            }
            finally
            {
                core.Dispose();
            }
        }
    }
}
