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
    /// Tests for what the core does with the activities of a plan and PlanGraph carries out: the edits, with the busy signal
    /// that covers each of them and what the core does about the plan afterwards, the settings that the core pushes into the
    /// activities, and the views of the activities that the UI binds to.
    /// </summary>
    /// <remarks>
    /// The user interface makes most of the edits. A command line job never does, and so nothing else reaches them.
    /// </remarks>
    public class CoreViewModelEditTests
    {
        private const int c_ActivityCount = 3;

        [Fact]
        public void AddManagedActivities_Given_Models_Then_TheBusySignalIsOnWhileTheyAreAddedAndOffAfter()
        {
            using CoreViewModel core = CreateCore();
            List<bool> busy = WatchBusy(core);

            core.AddManagedActivities([Model(4), Model(5)]);

            busy.ShouldBe(new[] { true, false });
            core.IsBusy.ShouldBeFalse();
            core.RawActivities.Select(x => x.Id).ShouldBe(new[] { 1, 2, 3, 4, 5 });
        }

        [Fact]
        public void RemoveManagedActivities_Given_SomeIds_Then_TheBusySignalIsOnWhileTheyAreRemovedAndOffAfter()
        {
            using CoreViewModel core = CreateCore();
            List<bool> busy = WatchBusy(core);

            core.RemoveManagedActivities([2]);

            busy.ShouldBe(new[] { true, false });
            core.IsBusy.ShouldBeFalse();
            core.RawActivities.Select(x => x.Id).ShouldBe(new[] { 1, 3 });
            core.IsProjectScenarioUpdated.ShouldBeTrue();
        }

        [Fact]
        public void UpdateManagedActivities_Given_AnEdit_Then_TheBusySignalIsOnWhileItIsMadeAndOffAfter()
        {
            using CoreViewModel core = CreateCore();
            List<bool> busy = WatchBusy(core);

            core.UpdateManagedActivities([new UpdateDependentActivityModel { Id = 2, IsNameEdited = true, Name = "Renamed" }]);

            busy.ShouldBe(new[] { true, false });
            core.IsBusy.ShouldBeFalse();
            core.RawActivities.Single(x => x.Id == 2).Name.ShouldBe("Renamed");
            core.RawActivities.Single(x => x.Id == 1).Name.ShouldBe("Activity 1");
            core.IsProjectScenarioUpdated.ShouldBeTrue();
        }

        [Fact]
        public void UpdateManagedActivities_Given_AutomaticCompilation_Then_ThePlanIsReadyToCompile()
        {
            using CoreViewModel core = CreateCore();
            core.AutoCompile = true;
            core.IsReadyToCompile.ShouldBe(ReadyToCompile.No);

            core.UpdateManagedActivities([new UpdateDependentActivityModel { Id = 2, IsNameEdited = true, Name = "Renamed" }]);

            core.IsReadyToCompile.ShouldBe(ReadyToCompile.Yes);
            core.IsReadyToReviseTrackers.ShouldBe(ReadyToRevise.Yes);
        }

        [Fact]
        public void UpdateManagedActivities_Given_NoAutomaticCompilation_Then_TheTrackersAreNotRevised()
        {
            using CoreViewModel core = CreateCore();
            core.AutoCompile = false;

            core.UpdateManagedActivities([new UpdateDependentActivityModel { Id = 2, IsNameEdited = true, Name = "Renamed" }]);

            // The core arms the revision of the trackers with the compilation, and only when automatic compilation is on.
            core.IsReadyToReviseTrackers.ShouldBe(ReadyToRevise.No);
        }

        [Fact]
        public void ClearManagedActivities_Given_ActivitiesInThePlan_Then_TheBusySignalIsOnWhileTheyGoAndOffAfter()
        {
            using CoreViewModel core = CreateCore();
            List<bool> busy = WatchBusy(core);

            core.ClearManagedActivities();

            busy.ShouldBe(new[] { true, false });
            core.IsBusy.ShouldBeFalse();
            core.RawActivities.ShouldBeEmpty();
            core.GetNextActivityId().ShouldBe(1);
        }

        [Fact]
        public void AddMilestone_Given_AnActivity_Then_AMilestoneThatDependsOnItComesBeforeItsSuccessors()
        {
            using CoreViewModel core = CreateCore();
            List<bool> busy = WatchBusy(core);
            core.HasCompilationErrors.ShouldBeFalse();

            core.AddMilestone([2]);

            busy.ShouldBe(new[] { true, false });
            core.RawActivities.Count.ShouldBe(c_ActivityCount + 1);
            IManagedActivityViewModel milestone = core.RawActivities.Single(x => x.Id == 4);
            milestone.Dependencies.ShouldBe(new HashSet<int> { 2 });
            core.RawActivities.Single(x => x.Id == 3).Dependencies.ShouldBe(new HashSet<int> { 2, 4 });
            core.RawActivities.Single(x => x.Id == 2).Dependencies.ShouldBe(new HashSet<int> { 1 });
            core.IsProjectScenarioUpdated.ShouldBeTrue();
        }

        [Fact]
        public void AddMilestone_Given_APlanThatDoesNotCompile_Then_AddsNothing()
        {
            // Two activities that depend on each other.
            using CoreViewModel core = CreateCore([Model(1, dependencies: [2]), Model(2, dependencies: [1])]);
            core.HasCompilationErrors.ShouldBeTrue("the plan was meant to have a cycle");

            core.AddMilestone([1]);

            core.RawActivities.Count.ShouldBe(2);
        }

        [Fact]
        public void RunTransitiveReduction_Given_ADependencyThatAnotherImplies_Then_ItIsRemovedAndThePlanIsCompiled()
        {
            using CoreViewModel core = CreateCore(
                [
                    Model(1),
                    Model(2, dependencies: [1]),
                    Model(3, dependencies: [1, 2]),
                ]);
            List<bool> busy = WatchBusy(core);
            object before = core.GraphCompilation;

            core.RunTransitiveReduction();

            core.RawActivities.Single(x => x.Id == 3).Dependencies.ShouldBe(new HashSet<int> { 2 });
            core.HasCompilationErrors.ShouldBeFalse();
            ReferenceEquals(core.GraphCompilation, before).ShouldBeFalse("the plan was not compiled again");
            busy.ShouldBe(new[] { true, false });
        }

        [Fact]
        public void GetNextActivityId_Given_ActivitiesInThePlan_Then_IsAnIdThatNoneOfThemHas()
        {
            using CoreViewModel core = CreateCore();

            int next = core.GetNextActivityId();

            new[] { 1, 2, 3 }.ShouldNotContain(next);
        }

        [Fact]
        public void UpdateActivityDisplayOrders_Given_ActivitiesMovedInTheView_Then_EachTakesItsPlace()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.StartSubscriptions();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            SequencerScopes.Drain(main, pool);
            core.OrderableActivities.Select(x => x.Id).ShouldBe(new[] { 1, 2, 3 });

            core.OrderableActivities.Move(0, 2);
            core.UpdateActivityDisplayOrders();

            core.RawActivities.OrderBy(x => x.DisplayOrder).Select(x => x.Id).ShouldBe(new[] { 2, 3, 1 });
        }

        [Fact]
        public void ResourceSettings_Given_SettingsWithoutAResource_Then_EveryActivityDropsItAsATarget()
        {
            // The activities of the chain target the two resources in turn: the first targets the second resource.
            using CoreViewModel core = CreateCore();
            core.RawActivities.Single(x => x.Id == 1).TargetResources.ShouldBe(new HashSet<int> { 2 });
            core.RawActivities.Single(x => x.Id == 2).TargetResources.ShouldBe(new HashSet<int> { 1 });

            core.ResourceSettings = core.ResourceSettings with
            {
                Resources = [.. core.ResourceSettings.Resources.Where(x => x.Id == 1)],
            };

            core.RawActivities.Single(x => x.Id == 1).TargetResources.ShouldBeEmpty();
            core.RawActivities.Single(x => x.Id == 2).TargetResources.ShouldBe(new HashSet<int> { 1 });
        }

        [Fact]
        public void WorkStreamSettings_Given_SettingsWithoutAWorkStream_Then_EveryActivityDropsItAsATarget()
        {
            using CoreViewModel core = CreateCoreWithWorkStreams();
            core.RawActivities.ShouldAllBe(x => x.TargetWorkStreams.Contains(2));

            core.WorkStreamSettings = new WorkStreamSettingsModel
            {
                WorkStreams = [.. core.WorkStreamSettings.WorkStreams.Where(x => x.Id == 1)],
            };

            core.RawActivities.ShouldAllBe(x => x.TargetWorkStreams.Count == 0);
        }

        [Fact]
        public void KillSubscriptions_Given_AStartedCore_Then_TheViewsOfTheActivitiesTakeNoMore()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.StartSubscriptions();
            core.AddManagedActivities([Model(1)]);
            SequencerScopes.Drain(main, pool);
            core.Activities.Select(x => x.Id).ShouldBe(new[] { 1 }, "the view was not filling to begin with");
            core.OrderableActivities.Select(x => x.Id).ShouldBe(new[] { 1 }, "the view was not filling to begin with");

            core.KillSubscriptions();
            core.AddManagedActivities([Model(2)]);
            SequencerScopes.Drain(main, pool);

            core.Activities.Select(x => x.Id).ShouldBe(new[] { 1 });
            core.OrderableActivities.Select(x => x.Id).ShouldBe(new[] { 1 });
        }

        // A core that holds a chain of three activities, each of them aimed at the second of two work streams.
        private static CoreViewModel CreateCoreWithWorkStreams()
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(c_ActivityCount);

            scenario = scenario with
            {
                WorkStreamSettings = new WorkStreamSettingsModel
                {
                    WorkStreams =
                    [
                        new WorkStreamModel { Id = 1, Name = "Work stream 1", DisplayOrder = 1 },
                        new WorkStreamModel { Id = 2, Name = "Work stream 2", DisplayOrder = 2 },
                    ],
                },
                DependentActivities =
                [
                    .. scenario.DependentActivities.Select(x => x with { Activity = x.Activity with { TargetWorkStreams = [2] } }),
                ],
            };

            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");
            return core;
        }

        // A core that holds a chain of three activities, or the ones given, and has compiled them.
        private static CoreViewModel CreateCore(IReadOnlyList<DependentActivityModel>? activities = null)
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(c_ActivityCount);

            if (activities is not null)
            {
                scenario = scenario with { DependentActivities = [.. activities] };
            }

            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");
            return core;
        }

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
    }
}
