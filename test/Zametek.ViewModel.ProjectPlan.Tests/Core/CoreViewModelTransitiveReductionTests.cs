using Shouldly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for RunTransitiveReduction. It takes away the dependencies that the user entered, and that other dependencies of
    /// the user imply. It works on the dependencies of the user alone: the dependencies that a resource gives are not part of
    /// the plan that it reduces, because a compilation makes them again and they change with the resources.
    /// </summary>
    /// <remarks>
    /// The plans here have one resource, and two activities that follow the same activity. The resource puts one of them
    /// behind the other, and so a compilation gives that one a dependency that the user did not enter.
    /// </remarks>
    public class CoreViewModelTransitiveReductionTests
    {
        #region What it takes away

        [Fact]
        public void RunTransitiveReduction_Given_ADependencyThatAnotherImpliesAndOneThatAResourceGives_Then_TakesAwayTheFirstOnly()
        {
            // 4 depends on 1 as well as on 2 and 3, which makes 4 depend on 1 twice. The resource queues 2, 3 and 4.
            using CoreViewModel core = CreateCore(Model(1), Model(2, [1]), Model(3, [1]), Model(4, [1, 2, 3]));
            Activity(core, 3).ResourceDependencies.ShouldContain(2, "the plan was meant to have a dependency that only the resource gives");

            core.RunTransitiveReduction();

            Activity(core, 4).Dependencies.ShouldBe(new HashSet<int> { 2, 3 });
            Activity(core, 3).Dependencies.ShouldBe(new HashSet<int> { 1 });
            Activity(core, 2).Dependencies.ShouldBe(new HashSet<int> { 1 });
        }

        [Fact]
        public void RunTransitiveReduction_Given_APlanningDependencyThatAnotherImplies_Then_TakesItAway()
        {
            using CoreViewModel core = CreateCore(Model(1), Model(2, [1]), Model(3, [2], planningDependencies: [1]));

            core.RunTransitiveReduction();

            Activity(core, 3).Dependencies.ShouldBe(new HashSet<int> { 2 });
            Activity(core, 3).PlanningDependencies.ShouldBeEmpty();
        }

        [Fact]
        public void RunTransitiveReduction_Given_NothingToTakeAway_Then_ChangesNoDependency()
        {
            using CoreViewModel core = CreateQueuedCore();
            string before = Dependencies(core);

            core.RunTransitiveReduction();

            Dependencies(core).ShouldBe(before);
        }

        [Fact]
        public void RunTransitiveReduction_Given_NoActivities_Then_ThereIsNothingToDoAndNoError()
        {
            using CoreViewModel core = CreateCore();

            Should.NotThrow(core.RunTransitiveReduction);

            core.RawActivities.ShouldBeEmpty();
        }

        #endregion

        #region What it does not take away

        [Fact]
        public void RunTransitiveReduction_Given_TheDependenciesOfAnActivityEditedAfterTheCompilation_Then_TheDependenciesOfTheUserStay()
        {
            using CoreViewModel core = CreateQueuedCore();
            Activity(core, 3).ResourceDependencies.ShouldContain(2, "the plan was meant to have a dependency that only the resource gives");

            // The user types the dependencies of 3 again. The graph of the core is told, and what it was told must not make
            // the dependency of the resource of 3 on 2 into a dependency of the user, that makes 3 on 1 look redundant.
            Activity(core, 3).DependenciesString = "1";
            core.RunTransitiveReduction();

            Activity(core, 3).Dependencies.ShouldBe(new HashSet<int> { 1 });
        }

        [Fact]
        public void RunTransitiveReduction_Given_ThePlanningDependenciesOfAnActivityEditedAfterTheCompilation_Then_TheyStay()
        {
            using CoreViewModel core = CreateCore(Model(1), Model(2, [1]), Model(3, planningDependencies: [1]));
            Activity(core, 3).ResourceDependencies.ShouldContain(2, "the plan was meant to have a dependency that only the resource gives");

            Activity(core, 3).PlanningDependenciesString = "1";
            core.RunTransitiveReduction();

            Activity(core, 3).PlanningDependencies.ShouldBe(new HashSet<int> { 1 });
        }

        [Fact]
        public void RunTransitiveReduction_Given_TwoReductionsInARow_Then_TheDependenciesOfTheUserStay()
        {
            using CoreViewModel core = CreateQueuedCore();

            core.RunTransitiveReduction();
            core.RunTransitiveReduction();

            Activity(core, 3).Dependencies.ShouldBe(new HashSet<int> { 1 });
        }

        #endregion

        #region A plan it cannot reduce

        [Fact]
        public void RunTransitiveReduction_Given_APlanThatDoesNotCompile_Then_RefusesWithTheErrorsAndChangesNothing()
        {
            // Two activities that depend on each other.
            using CoreViewModel core = CreateCore(Model(1, [2]), Model(2, [1]));
            core.HasCompilationErrors.ShouldBeTrue("the plan was meant to have a cycle");
            object compilation = core.GraphCompilation;
            string before = Describe(core);
            List<bool> busy = WatchBusy(core);

            InvalidOperationException refusal = Should.Throw<InvalidOperationException>(core.RunTransitiveReduction);

            refusal.Message.ShouldStartWith(Resource.ProjectPlan.Messages.Message_TransitiveReductionCompilationErrors);
            core.GraphCompilation.CompilationErrors.ShouldNotBeEmpty();
            foreach (IGraphCompilationError error in core.GraphCompilation.CompilationErrors)
            {
                refusal.Message.ShouldContain($@"{error.ErrorCode}: {error.ErrorMessage}");
            }

            Describe(core).ShouldBe(before);
            ReferenceEquals(core.GraphCompilation, compilation).ShouldBeTrue("the plan was compiled again");
            busy.ShouldBe(new[] { true, false });
        }

        [Fact]
        public void RunTransitiveReduction_Given_AnActivityThatDependsOnOneThatIsNotThere_Then_RefusesWithTheErrorsAndChangesNothing()
        {
            using CoreViewModel core = CreateCore(Model(1), Model(2, [1, 9]));
            core.HasCompilationErrors.ShouldBeTrue("the plan was meant to have a dependency on an activity that is not there");
            string before = Describe(core);

            InvalidOperationException refusal = Should.Throw<InvalidOperationException>(core.RunTransitiveReduction);

            refusal.Message.ShouldStartWith(Resource.ProjectPlan.Messages.Message_TransitiveReductionCompilationErrors);
            core.GraphCompilation.CompilationErrors.ShouldNotBeEmpty();
            foreach (IGraphCompilationError error in core.GraphCompilation.CompilationErrors)
            {
                refusal.Message.ShouldContain($@"{error.ErrorCode}: {error.ErrorMessage}");
            }

            Describe(core).ShouldBe(before);
        }

        #endregion

        #region What it does with the compilations

        [Fact]
        public void RunTransitiveReduction_Then_PublishesTheResultsOfOneCompilation()
        {
            using CoreViewModel core = CreateQueuedCore();
            int announced = 0;
            ((INotifyPropertyChanged)Activity(core, 3)).PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IManagedActivityViewModel.EarliestStartTime))
                {
                    announced++;
                }
            };

            core.RunTransitiveReduction();

            // The compilation that checks the plan before the reduction works on a copy, and its results go nowhere.
            announced.ShouldBe(1);
        }

        [Fact]
        public async Task RunTransitiveReduction_Given_AnEditWhileItWorks_Then_ChangesNothingAndSaysSo()
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGated();

            using (core)
            {
                // 3 depends on 1 as well as on 2, and so a reduction that begins now takes the dependency on 1 away.
                core.ProcessProjectScenario(Scenario([Model(1), Model(2, [1]), Model(3, [1, 2])]), Guid.NewGuid(), @"Test");
                core.HoldNextCompilation();
                Task reduction = Task.Run(core.RunTransitiveReduction);
                core.WaitUntilHeld();

                // The user takes the dependency of 2 on 1 away, while the copy of the plan is compiled. That makes the
                // dependency of 3 on 1 needed, and the reduction does not know.
                Activity(core, 2).DependenciesString = string.Empty;
                core.ReleaseHeldCompilation();

                InvalidOperationException refusal = await Should.ThrowAsync<InvalidOperationException>(reduction);
                refusal.Message.ShouldBe(Resource.ProjectPlan.Messages.Message_TransitiveReductionPlanChanged);
                Activity(core, 3).Dependencies.ShouldBe(new HashSet<int> { 1, 2 });
                Activity(core, 2).Dependencies.ShouldBeEmpty();
            }
        }

        [Fact]
        public void RunTransitiveReduction_Given_ACompilationThatOutlastsTheWatchdog_Then_AbandonsItAndChangesNothing()
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGated(compilationTimeoutMilliseconds: 1_000);

            using (core)
            {
                // 3 depends on 1 as well as on 2, and so a reduction that is not cut short takes the dependency on 1 away.
                core.ProcessProjectScenario(Scenario([Model(1), Model(2, [1]), Model(3, [1, 2])]), Guid.NewGuid(), @"Test");
                string before = Describe(core);
                core.HasStaleOutputs.ShouldBeFalse("the plan was meant to be compiled");
                core.StallNextCompilation();

                Should.Throw<GraphCompilationTimeoutException>(core.RunTransitiveReduction);

                Describe(core).ShouldBe(before);
                core.HasStaleOutputs.ShouldBeTrue();
            }
        }

        #endregion

        #region Helpers

        // A core that holds activities 1, 2 and 3, the last two after the first, and has compiled them. The resource queues 3
        // behind 2.
        private static CoreViewModel CreateQueuedCore() => CreateCore(Model(1), Model(2, [1]), Model(3, [1]));

        // A core that has compiled the activities. It has one resource, and so the activities that are ready together queue
        // on it.
        private static CoreViewModel CreateCore(params DependentActivityModel[] activities)
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(Scenario(activities), Guid.NewGuid(), @"Test");
            return core;
        }

        private static ProjectScenarioModel Scenario(IEnumerable<DependentActivityModel> activities)
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(0);

            return scenario with
            {
                ResourceSettings = scenario.ResourceSettings with
                {
                    Resources = [.. scenario.ResourceSettings.Resources.Where(x => x.Id == 1)],
                },
                DependentActivities = [.. activities],
            };
        }

        private static DependentActivityModel Model(
            int id,
            List<int>? dependencies = null,
            List<int>? planningDependencies = null) =>
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
                PlanningDependencies = planningDependencies ?? [],
            };

        private static IManagedActivityViewModel Activity(CoreViewModel core, int id) =>
            core.RawActivities.Single(x => x.Id == id);

        private static string Set(IEnumerable<int> ids) => string.Join(',', ids.Order());

        // The dependencies of the user, one activity to a line.
        private static string Dependencies(CoreViewModel core) =>
            string.Join(
                Environment.NewLine,
                core.RawActivities
                    .OrderBy(x => x.Id)
                    .Select(x => $@"{x.Id}: [{Set(x.Dependencies)}] [{Set(x.PlanningDependencies)}]"));

        // Everything that a compilation or a reduction can change in an activity, one activity to a line.
        private static string Describe(CoreViewModel core) =>
            string.Join(
                Environment.NewLine,
                core.RawActivities
                    .OrderBy(x => x.Id)
                    .Select(x => $@"{x.Id}: [{Set(x.Dependencies)}] [{Set(x.PlanningDependencies)}] [{Set(x.ResourceDependencies)}] [{Set(x.Successors)}] [{Set(x.AllocatedToResources)}] {x.EarliestStartTime} {x.LatestFinishTime} {x.FreeSlack}"));

        // The value of the busy signal each time that the core raises it.
        private static List<bool> WatchBusy(CoreViewModel core)
        {
            List<bool> busy = [];

            core.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ICoreViewModel.IsBusy))
                {
                    busy.Add(core.IsBusy);
                }
            };

            return busy;
        }

        #endregion
    }
}
