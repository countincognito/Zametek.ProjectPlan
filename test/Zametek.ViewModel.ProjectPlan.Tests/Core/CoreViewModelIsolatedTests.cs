using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the Isolated flag of an activity, through the core: a plan is loaded and compiled, the user edits the
    /// dependencies of an activity, and the plan is compiled again.
    /// </summary>
    /// <remarks>
    /// The flag says that an activity has no dependency and no successor that the user entered. A resource that makes an
    /// activity wait for another one gives it a resource dependency, and that is a result of a compilation and not a link of
    /// the plan. Version 0.9.3 showed such an activity as isolated after every compilation, because the compilation took
    /// the links of the resources out of the graph that the flag reads. The compilation works on a copy now, so the graph
    /// has to keep them out itself.
    /// </remarks>
    public class CoreViewModelIsolatedTests
    {
        [Fact]
        public void IsIsolated_Given_TwoActivitiesThatShareAResource_Then_BothAreIsolatedAlthoughOneWaitsForTheOther()
        {
            using CoreViewModel core = CreateCore(Model(1), Model(2));

            Activity(core, 2).ResourceDependencies.ShouldBe(new HashSet<int> { 1 }, "the test cannot tell a link of a resource from none");
            Activity(core, 1).IsIsolated.ShouldBeTrue();
            Activity(core, 2).IsIsolated.ShouldBeTrue();
        }

        [Fact]
        public void IsIsolated_Given_TheDependenciesOfAnActivityAreEnteredAgainAsNone_Then_BothAreStillIsolatedAfterTheCompilation()
        {
            using CoreViewModel core = CreateCore(Model(1), Model(2));

            Activity(core, 2).DependenciesString = string.Empty;
            core.RunCompile();

            Activity(core, 2).ResourceDependencies.ShouldBe(new HashSet<int> { 1 }, "the test cannot tell a link of a resource from none");
            Activity(core, 1).IsIsolated.ShouldBeTrue();
            Activity(core, 2).IsIsolated.ShouldBeTrue();
        }

        [Fact]
        public void IsIsolated_Given_ThePlanningDependenciesOfAnActivityAreEnteredAgainAsNone_Then_BothAreStillIsolatedAfterTheCompilation()
        {
            using CoreViewModel core = CreateCore(Model(1), Model(2));

            Activity(core, 2).PlanningDependenciesString = string.Empty;
            core.RunCompile();

            Activity(core, 2).ResourceDependencies.ShouldBe(new HashSet<int> { 1 }, "the test cannot tell a link of a resource from none");
            Activity(core, 1).IsIsolated.ShouldBeTrue();
            Activity(core, 2).IsIsolated.ShouldBeTrue();
        }

        [Fact]
        public void IsIsolated_Given_ADependencyIsEnteredAndThenTakenAway_Then_BothAreIsolatedAgainAfterTheCompilation()
        {
            using CoreViewModel core = CreateCore(Model(1), Model(2));

            // The resource gave the dependency already, and now the user enters it. It is a link of the plan from then on.
            Activity(core, 2).DependenciesString = "1";
            core.RunCompile();

            Activity(core, 2).Dependencies.ShouldBe(new HashSet<int> { 1 });
            Activity(core, 1).IsIsolated.ShouldBeFalse();
            Activity(core, 2).IsIsolated.ShouldBeFalse();

            // The resource still makes the second wait for the first, and the user has taken the link away.
            Activity(core, 2).DependenciesString = string.Empty;
            core.RunCompile();

            Activity(core, 2).Dependencies.ShouldBeEmpty();
            Activity(core, 2).ResourceDependencies.ShouldBe(new HashSet<int> { 1 }, "the test cannot tell a link of a resource from none");
            Activity(core, 1).IsIsolated.ShouldBeTrue();
            Activity(core, 2).IsIsolated.ShouldBeTrue();
        }

        [Fact]
        public void IsIsolated_Given_APlanningDependencyIsEnteredAndThenTakenAway_Then_BothAreIsolatedAgainAfterTheCompilation()
        {
            using CoreViewModel core = CreateCore(Model(1), Model(2));

            Activity(core, 2).PlanningDependenciesString = "1";
            core.RunCompile();

            Activity(core, 2).PlanningDependencies.ShouldBe(new HashSet<int> { 1 });
            Activity(core, 1).IsIsolated.ShouldBeFalse();
            Activity(core, 2).IsIsolated.ShouldBeFalse();

            Activity(core, 2).PlanningDependenciesString = string.Empty;
            core.RunCompile();

            Activity(core, 2).PlanningDependencies.ShouldBeEmpty();
            Activity(core, 1).IsIsolated.ShouldBeTrue();
            Activity(core, 2).IsIsolated.ShouldBeTrue();
        }

        [Fact]
        public void IsIsolated_Given_AMilestoneIsAdded_Then_AnActivityThatOnlyAResourceLinksStaysIsolated()
        {
            using CoreViewModel core = CreateCore(Model(1), Model(2, dependencies: [1]), Model(3));

            // The compilation put the second after the third in the queue of the resource, and wrote that as a resource
            // dependency of the second. The test writes it so, and does not leave it to the scheduler.
            Activity(core, 2).ResourceDependencies.Clear();
            Activity(core, 2).ResourceDependencies.Add(3);

            core.AddMilestone([1]);

            Activity(core, 2).Dependencies.ShouldBe(new HashSet<int> { 1, 4 });
            Activity(core, 1).IsIsolated.ShouldBeFalse();
            Activity(core, 2).IsIsolated.ShouldBeFalse();
            Activity(core, 4).IsIsolated.ShouldBeFalse();
            Activity(core, 3).IsIsolated.ShouldBeTrue("only the resource links it to the second");
        }

        // One resource and the activities that are given. The load compiles the plan, and the compilation gives a resource
        // dependency to every activity that the resource makes wait. The compilation is run once more for a test that
        // reads the result.
        private static CoreViewModel CreateCore(params DependentActivityModel[] activities)
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(0);
            scenario = scenario with
            {
                ResourceSettings = scenario.ResourceSettings with
                {
                    Resources = [.. scenario.ResourceSettings.Resources.Where(x => x.Id == 1)],
                },
                DependentActivities = [.. activities],
            };
            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");
            core.RunCompile();
            return core;
        }

        private static IManagedActivityViewModel Activity(CoreViewModel core, int id) =>
            core.RawActivities.Single(x => x.Id == id);

        private static DependentActivityModel Model(int id, List<int>? dependencies = null) =>
            new()
            {
                Activity = new ActivityModel
                {
                    Id = id,
                    DisplayOrder = id,
                    Name = $@"Activity {id}",
                    Duration = 2,
                },
                Dependencies = dependencies ?? [],
            };
    }
}
