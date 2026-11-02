using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the live plan that the core holds in PlanGraph: the activities, the compiler that holds them as its
    /// graph, the copies that a compilation and a simulation take, the results that a compilation gives back, and the
    /// views that the UI binds to.
    /// </summary>
    /// <remarks>
    /// PlanGraph needs no core of its own. The activities that it makes read their settings from a core, so a test gives
    /// it one that is never started, and that core is only the owner of the activities. The graph under test is a second
    /// one, and the core does not use it.
    /// </remarks>
    public class PlanGraphTests
    {
        // A Monday, the Tuesday after it and the Thursday of that week. No weekend falls between them, and so a count of the
        // days from one to another changes only when a holiday is introduced.
        private static readonly DateTimeOffset s_Monday = Local(2026, 1, 5);
        private static readonly DateTimeOffset s_Tuesday = Local(2026, 1, 6);
        private static readonly DateTimeOffset s_Thursday = Local(2026, 1, 8);

        #region Construction

        [Fact]
        public void Constructor_Given_ANullMapper_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => new PlanGraph(null!, new DateTimeCalculator(TimeProvider.System), new SubscriptionLifetime()));
        }

        [Fact]
        public void Constructor_Given_ANullDateTimeCalculator_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => new PlanGraph(new ProjectPlanMapper(), null!, new SubscriptionLifetime()));
        }

        [Fact]
        public void Constructor_Given_ANullSubscriptionLifetime_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => new PlanGraph(new ProjectPlanMapper(), new DateTimeCalculator(TimeProvider.System), null!));
        }

        [Fact]
        public void Constructor_Given_NothingMore_Then_TheGraphAndItsViewsAreEmpty()
        {
            using var harness = new Harness();

            harness.Graph.RawActivities.ShouldBeEmpty();
            harness.Graph.Activities.ShouldBeEmpty();
            harness.Graph.OrderableActivities.ShouldBeEmpty();
            harness.Graph.StartTime.ShouldBe(0);
            harness.Graph.FinishTime.ShouldBe(0);
        }

        #endregion

        #region Structural edits

        [Fact]
        public void GetNextActivityId_Given_ActivitiesInTheGraph_Then_IsAnIdThatNoneOfThemHas()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2), Model(3)]);

            int next = harness.Graph.GetNextActivityId();

            next.ShouldBeGreaterThan(0);
            new[] { 1, 2, 3 }.ShouldNotContain(next);
        }

        [Fact]
        public void AddActivities_Given_ModelsOutOfOrder_Then_TheyJoinTheListByDisplayOrderAndThenById()
        {
            using var harness = new Harness();

            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [Model(3, displayOrder: 1), Model(2, displayOrder: 2), Model(1, displayOrder: 2)]);

            harness.Graph.RawActivities.Select(x => x.Id).ShouldBe(new[] { 3, 1, 2 });
        }

        [Fact]
        public void AddActivities_Given_AnIdThatTheGraphAlreadyHolds_Then_TheListKeepsTheFirstActivityOnly()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1, name: "First")]);

            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1, name: "Second"), Model(2, name: "Other")]);

            harness.Graph.RawActivities.Select(x => (x.Id, Name: x.Name ?? string.Empty)).ShouldBe(new[] { (1, "First"), (2, "Other") });
        }

        [Fact]
        public void AddActivities_Given_AProjectStart_Then_EachActivityMeasuresItsConstraintsFromIt()
        {
            using var harness = new Harness();

            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1, minimumEarliestStart: s_Thursday)]);
            harness.Graph.AddActivities(harness.Owner, s_Tuesday, [Model(2, minimumEarliestStart: s_Thursday)]);

            harness.Graph.RawActivities.Single(x => x.Id == 1).MinimumEarliestStartTime.ShouldBe(3);
            harness.Graph.RawActivities.Single(x => x.Id == 2).MinimumEarliestStartTime.ShouldBe(2);
        }

        [Fact]
        public void AddActivities_Given_ModelsWithDependencies_Then_TheActivitiesHaveThem()
        {
            using var harness = new Harness();

            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2, dependencies: [1])]);

            harness.Graph.RawActivities.Single(x => x.Id == 1).Dependencies.ShouldBeEmpty();
            harness.Graph.RawActivities.Single(x => x.Id == 2).Dependencies.ShouldBe(new HashSet<int> { 1 });
        }

        [Fact]
        public void RemoveActivities_Given_SomeIds_Then_OnlyThoseActivitiesLeaveTheListAndTheGraph()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2), Model(3)]);

            harness.Graph.RemoveActivities([2, 9]);

            harness.Graph.RawActivities.Select(x => x.Id).ShouldBe(new[] { 1, 3 });

            // The graph has forgotten the activity, so another can take its id.
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(2, name: "Again")]);
            harness.Graph.RawActivities.Select(x => x.Id).ShouldBe(new[] { 1, 3, 2 });
        }

        [Fact]
        public void RemoveActivities_Given_AnActivity_Then_ItIsDisposed()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);
            IManagedActivityViewModel kept = harness.Graph.RawActivities.Single(x => x.Id == 1);
            IManagedActivityViewModel removed = harness.Graph.RawActivities.Single(x => x.Id == 2);

            harness.Graph.RemoveActivities([2]);

            // A live activity marks itself as compiled when its owner publishes a compilation. A disposed one does not.
            harness.Owner.RunCompile();
            kept.IsCompiled.ShouldBeTrue("the test cannot tell a disposed activity from a live one");
            removed.IsCompiled.ShouldBeFalse();
        }

        [Fact]
        public void ClearActivities_Given_ActivitiesInTheGraph_Then_TheListIsEmptyTheGraphIsResetAndTheActivitiesAreDisposed()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);
            List<IManagedActivityViewModel> cleared = [.. harness.Graph.RawActivities];

            harness.Graph.ClearActivities();

            harness.Graph.RawActivities.ShouldBeEmpty();
            harness.Graph.GetNextActivityId().ShouldBe(1);

            // The graph has been reset, so it takes the id again.
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1)]);
            harness.Graph.RawActivities.Select(x => x.Id).ShouldBe(new[] { 1 });

            harness.Owner.RunCompile();
            harness.Graph.RawActivities.Single().IsCompiled.ShouldBeTrue("the test cannot tell a disposed activity from a live one");
            cleared.ShouldAllBe(x => !x.IsCompiled);
        }

        [Fact]
        public void SetActivityDependencies_Given_BothKinds_Then_TheActivityHoldsEachInItsOwnSet()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2), Model(3)]);

            harness.Graph.SetActivityDependencies(3, [1], [2]);

            IManagedActivityViewModel activity = harness.Graph.RawActivities.Single(x => x.Id == 3);
            activity.Dependencies.ShouldBe(new HashSet<int> { 1 });
            activity.PlanningDependencies.ShouldBe(new HashSet<int> { 2 });
        }

        #endregion

        #region Reduction

        // Three activities, the second after the first and the third after both, copied as a reduction takes them. The
        // reduction that the tests apply takes the dependency of the third on the first away.
        private static (Harness Harness, Dictionary<int, IManagedActivityViewModel> Sources, TransitiveReduction Reduction) Reducible()
        {
            var harness = new Harness();
            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [Model(1), Model(2, dependencies: [1]), Model(3, dependencies: [1, 2])]);
            (_, Dictionary<int, IManagedActivityViewModel> sources) = harness.Graph.CopyActivities();
            var reduction = new TransitiveReduction(
                Compile([]),
                new Dictionary<int, ActivityDependencies>
                {
                    [1] = Dependencies(),
                    [2] = Dependencies(1),
                    [3] = Dependencies(1, 2),
                },
                new Dictionary<int, ActivityDependencies>
                {
                    [1] = Dependencies(),
                    [2] = Dependencies(1),
                    [3] = Dependencies(2),
                });

            return (harness, sources, reduction);
        }

        private static ActivityDependencies Dependencies(params int[] dependencies) => new([.. dependencies], []);

        private static IManagedActivityViewModel Activity(Harness harness, int id) =>
            harness.Graph.RawActivities.Single(x => x.Id == id);

        [Fact]
        public void TryApplyReduction_Given_ThePlanAsItWasCopied_Then_TheActivitiesTakeTheReducedDependencies()
        {
            (Harness harness, Dictionary<int, IManagedActivityViewModel> sources, TransitiveReduction reduction) = Reducible();
            using (harness)
            {
                bool applied = harness.Graph.TryApplyReduction(reduction, sources);

                applied.ShouldBeTrue();
                Activity(harness, 3).Dependencies.ShouldBe(new HashSet<int> { 2 });
                Activity(harness, 2).Dependencies.ShouldBe(new HashSet<int> { 1 });
                Activity(harness, 1).Dependencies.ShouldBeEmpty();
            }
        }

        [Fact]
        public void TryApplyReduction_Given_APlanningDependencyAway_Then_TheActivityLosesIt()
        {
            (Harness harness, Dictionary<int, IManagedActivityViewModel> sources, TransitiveReduction reduction) = Reducible();
            using (harness)
            {
                harness.Graph.SetActivityDependencies(3, [2], [1]);
                (_, sources) = harness.Graph.CopyActivities();
                reduction = new TransitiveReduction(
                    reduction.Compilation,
                    new Dictionary<int, ActivityDependencies>
                    {
                        [1] = Dependencies(),
                        [2] = Dependencies(1),
                        [3] = new([2], [1]),
                    },
                    new Dictionary<int, ActivityDependencies>
                    {
                        [1] = Dependencies(),
                        [2] = Dependencies(1),
                        [3] = Dependencies(2),
                    });

                harness.Graph.TryApplyReduction(reduction, sources).ShouldBeTrue();

                Activity(harness, 3).Dependencies.ShouldBe(new HashSet<int> { 2 });
                Activity(harness, 3).PlanningDependencies.ShouldBeEmpty();
            }
        }

        [Fact]
        public void TryApplyReduction_Given_ADependencyAway_Then_TheLiveCompilerFollows()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2, dependencies: [1])]);
            (_, Dictionary<int, IManagedActivityViewModel> sources) = harness.Graph.CopyActivities();
            Activity(harness, 2).IsIsolated.ShouldBeFalse("the test cannot tell an edge from none");

            // Not a reduction that a compilation would make, but the live graph has to do as it is told.
            bool applied = harness.Graph.TryApplyReduction(
                new TransitiveReduction(
                    Compile([]),
                    new Dictionary<int, ActivityDependencies> { [1] = Dependencies(), [2] = Dependencies(1) },
                    new Dictionary<int, ActivityDependencies> { [1] = Dependencies(), [2] = Dependencies() }),
                sources);

            applied.ShouldBeTrue();
            Activity(harness, 1).IsIsolated.ShouldBeTrue();
            Activity(harness, 2).IsIsolated.ShouldBeTrue();
        }

        [Fact]
        public void TryApplyReduction_Given_AnActivityEditedSinceTheCopy_Then_ChangesNothingAndSaysSo()
        {
            (Harness harness, Dictionary<int, IManagedActivityViewModel> sources, TransitiveReduction reduction) = Reducible();
            using (harness)
            {
                // The user takes the dependency of the second on the first away. That makes the dependency of the third on
                // the first needed, and the reduction does not know.
                Activity(harness, 2).DependenciesString = string.Empty;

                bool applied = harness.Graph.TryApplyReduction(reduction, sources);

                applied.ShouldBeFalse();
                Activity(harness, 3).Dependencies.ShouldBe(new HashSet<int> { 1, 2 });
                Activity(harness, 2).Dependencies.ShouldBeEmpty();
            }
        }

        [Fact]
        public void TryApplyReduction_Given_ADependencyAddedSinceTheCopy_Then_ChangesNothingAndSaysSo()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [Model(1), Model(2, dependencies: [1]), Model(3, dependencies: [1, 2]), Model(4)]);
            (_, Dictionary<int, IManagedActivityViewModel> sources) = harness.Graph.CopyActivities();
            var reduction = new TransitiveReduction(
                Compile([]),
                new Dictionary<int, ActivityDependencies>
                {
                    [1] = Dependencies(),
                    [2] = Dependencies(1),
                    [3] = Dependencies(1, 2),
                    [4] = Dependencies(),
                },
                new Dictionary<int, ActivityDependencies>
                {
                    [1] = Dependencies(),
                    [2] = Dependencies(1),
                    [3] = Dependencies(2),
                    [4] = Dependencies(),
                });

            // The user makes the third wait for the fourth too. An activity that holds more than it held when it was
            // copied is not the activity that the reduction saw.
            Activity(harness, 3).DependenciesString = "1,2,4";

            harness.Graph.TryApplyReduction(reduction, sources).ShouldBeFalse();
            Activity(harness, 3).Dependencies.ShouldBe(new HashSet<int> { 1, 2, 4 });
        }

        [Fact]
        public void TryApplyReduction_Given_AnActivityThatTheReductionLeftAloneAndWasEdited_Then_ChangesNothingAndSaysSo()
        {
            (Harness harness, Dictionary<int, IManagedActivityViewModel> sources, TransitiveReduction reduction) = Reducible();
            using (harness)
            {
                // The second activity has the same dependencies before and after the reduction. The edit of its planning
                // dependencies is still an edit of the plan that the reduction was made from.
                Activity(harness, 2).PlanningDependenciesString = "1";

                harness.Graph.TryApplyReduction(reduction, sources).ShouldBeFalse();
                Activity(harness, 3).Dependencies.ShouldBe(new HashSet<int> { 1, 2 });
            }
        }

        [Fact]
        public void TryApplyReduction_Given_AnActivityReplacedSinceTheCopy_Then_ChangesNothingAndSaysSo()
        {
            (Harness harness, Dictionary<int, IManagedActivityViewModel> sources, TransitiveReduction reduction) = Reducible();
            using (harness)
            {
                // Another activity with the same id, and the same dependencies.
                harness.Graph.RemoveActivities([3]);
                harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(3, dependencies: [1, 2])]);

                bool applied = harness.Graph.TryApplyReduction(reduction, sources);

                applied.ShouldBeFalse();
                Activity(harness, 3).Dependencies.ShouldBe(new HashSet<int> { 1, 2 });
            }
        }

        [Fact]
        public void TryApplyReduction_Given_AnActivityRemovedSinceTheCopy_Then_ChangesNothingAndSaysSo()
        {
            (Harness harness, Dictionary<int, IManagedActivityViewModel> sources, TransitiveReduction reduction) = Reducible();
            using (harness)
            {
                harness.Graph.RemoveActivities([2]);

                harness.Graph.TryApplyReduction(reduction, sources).ShouldBeFalse();
                Activity(harness, 3).Dependencies.ShouldBe(new HashSet<int> { 1 });
            }
        }

        [Fact]
        public void TryApplyReduction_Given_AnActivityAddedSinceTheCopy_Then_ChangesNothingAndSaysSo()
        {
            (Harness harness, Dictionary<int, IManagedActivityViewModel> sources, TransitiveReduction reduction) = Reducible();
            using (harness)
            {
                harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(4)]);

                harness.Graph.TryApplyReduction(reduction, sources).ShouldBeFalse();
                Activity(harness, 3).Dependencies.ShouldBe(new HashSet<int> { 1, 2 });
            }
        }

        [Fact]
        public void TryApplyReduction_Given_NullArguments_Then_Throws()
        {
            (Harness harness, Dictionary<int, IManagedActivityViewModel> sources, TransitiveReduction reduction) = Reducible();
            using (harness)
            {
                Should.Throw<ArgumentNullException>(() => harness.Graph.TryApplyReduction(null!, sources));
                Should.Throw<ArgumentNullException>(() => harness.Graph.TryApplyReduction(reduction, null!));
            }
        }

        #endregion

        #region Updates

        [Fact]
        public void UpdateActivities_Given_EveryKindOfEdit_Then_TheActivityTakesEachAndTheOthersAreLeftAlone()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [
                    Model(1, name: "A", targetResources: [1], targetWorkStreams: [1]),
                    Model(2, name: "B", targetResources: [1], targetWorkStreams: [1]),
                ]);
            harness.Graph.UpdateActivities(
                [
                    new UpdateDependentActivityModel
                    {
                        Id = 1,
                        IsNameEdited = true,
                        Name = "Renamed",
                        IsNotesEdited = true,
                        Notes = "Some notes",
                        IsTargetWorkStreamsEdited = true,
                        TargetWorkStreams = [2],
                        IsTargetResourcesEdited = true,
                        TargetResources = [2],
                        IsTargetResourceOperatorEdited = true,
                        TargetResourceOperator = LogicalOperator.OR,
                        IsHasNoCostEdited = true,
                        HasNoCost = true,
                        IsHasNoBillingEdited = true,
                        HasNoBilling = true,
                        IsHasNoEffortEdited = true,
                        HasNoEffort = true,
                        IsHasNoRiskEdited = true,
                        HasNoRisk = true,
                        IsOverrideColorEdited = true,
                        OverrideColor = true,
                        IsColorFormatEdited = true,
                        ColorFormat = new ColorFormatModel { A = 255, R = 1, G = 2, B = 3 },
                    },
                    new UpdateDependentActivityModel { Id = 99, IsNameEdited = true, Name = "Nobody" },
                ]);

            IManagedActivityViewModel updated = harness.Graph.RawActivities.Single(x => x.Id == 1);
            updated.Name.ShouldBe("Renamed");
            updated.Notes.ShouldBe("Some notes");
            updated.TargetWorkStreams.ShouldBe(new HashSet<int> { 2 });
            updated.TargetResources.ShouldBe(new HashSet<int> { 2 });
            updated.TargetResourceOperator.ShouldBe(LogicalOperator.OR);
            updated.HasNoCost.ShouldBeTrue();
            updated.HasNoBilling.ShouldBeTrue();
            updated.HasNoEffort.ShouldBeTrue();
            updated.HasNoRisk.ShouldBeTrue();
            updated.OverrideColor.ShouldBeTrue();
            updated.ColorFormat.ShouldBe(new ColorFormatModel { A = 255, R = 1, G = 2, B = 3 });

            IManagedActivityViewModel untouched = harness.Graph.RawActivities.Single(x => x.Id == 2);
            untouched.Name.ShouldBe("B");
            untouched.TargetResources.ShouldBe(new HashSet<int> { 1 });
            untouched.HasNoCost.ShouldBeFalse();
        }

        // Some of the properties of an activity commit an edit of their own when they are set, and the name does not. A name
        // that was edited is the case that needs the commit of the update, and the commit marks the plan of the owner as updated.
        [Fact]
        public void UpdateActivities_Given_ANameEditOnly_Then_TheEditIsCommittedAndThePlanOfTheOwnerIsMarkedAsUpdated()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1, name: "A")]);
            harness.Owner.IsProjectScenarioUpdated.ShouldBeFalse("the plan was updated before the edit");

            harness.Graph.UpdateActivities([new UpdateDependentActivityModel { Id = 1, IsNameEdited = true, Name = "Renamed" }]);

            harness.Graph.RawActivities.Single().Name.ShouldBe("Renamed");
            harness.Owner.IsProjectScenarioUpdated.ShouldBeTrue("the edit was not committed");
        }

        [Fact]
        public void UpdateActivities_Given_ValuesThatAreNotMarkedAsEdited_Then_NoneIsApplied()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [Model(1, name: "A", targetResources: [1], targetWorkStreams: [1])]);
            IManagedActivityViewModel activity = harness.Graph.RawActivities.Single();
            ColorFormatModel colorBefore = activity.ColorFormat;

            harness.Graph.UpdateActivities(
                [
                    new UpdateDependentActivityModel
                    {
                        Id = 1,
                        Name = "Renamed",
                        Notes = "Some notes",
                        TargetWorkStreams = [2],
                        TargetResources = [2],
                        TargetResourceOperator = LogicalOperator.OR,
                        HasNoCost = true,
                        HasNoBilling = true,
                        HasNoEffort = true,
                        HasNoRisk = true,
                        OverrideColor = true,
                        ColorFormat = new ColorFormatModel { A = 255, R = 1, G = 2, B = 3 },
                    },
                ]);

            activity.Name.ShouldBe("A");
            activity.Notes.ShouldBeNullOrEmpty();
            activity.TargetWorkStreams.ShouldBe(new HashSet<int> { 1 });
            activity.TargetResources.ShouldBe(new HashSet<int> { 1 });
            activity.TargetResourceOperator.ShouldNotBe(LogicalOperator.OR);
            activity.HasNoCost.ShouldBeFalse();
            activity.HasNoBilling.ShouldBeFalse();
            activity.HasNoEffort.ShouldBeFalse();
            activity.HasNoRisk.ShouldBeFalse();
            activity.OverrideColor.ShouldBeFalse();
            activity.ColorFormat.ShouldBe(colorBefore);
        }

        [Fact]
        public void UpdateActivities_Given_AnEdit_Then_TheActivityIsMutedWhileItIsMadeAndNotAfter()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1, name: "A")]);
            IManagedActivityViewModel activity = harness.Graph.RawActivities.Single();
            List<bool> muted = [];
            activity.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IManagedActivityViewModel.IsEditMuted))
                {
                    muted.Add(activity.IsEditMuted);
                }
            };

            harness.Graph.UpdateActivities([new UpdateDependentActivityModel { Id = 1, IsNameEdited = true, Name = "Renamed" }]);

            muted.ShouldBe(new[] { true, false });
            activity.IsEditMuted.ShouldBeFalse();
        }

        #endregion

        #region Pushes into the activities

        [Fact]
        public void SetProjectStart_Given_ANewStart_Then_EveryActivityMeasuresItsConstraintsFromIt()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [Model(1, minimumEarliestStart: s_Thursday), Model(2, minimumEarliestStart: s_Thursday)]);

            harness.Graph.SetProjectStart(s_Tuesday);

            harness.Graph.RawActivities.Select(x => x.ProjectStart).ShouldAllBe(x => x == s_Tuesday);
            harness.Graph.RawActivities.Select(x => x.MinimumEarliestStartTime).ShouldAllBe(x => x == 2);
        }

        [Fact]
        public void SetResourceSettings_Given_SettingsWithoutAResource_Then_EveryActivityDropsItAsATarget()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [Model(1, targetResources: [1, 2]), Model(2, targetResources: [1])]);

            harness.Graph.SetResourceSettings(
                new ResourceSettingsModel
                {
                    Resources = [harness.Owner.ResourceSettings.Resources.Single(x => x.Id == 2)],
                    DefaultUnitCost = 1.0,
                    DefaultUnitBilling = 1.0,
                });

            harness.Graph.RawActivities.Single(x => x.Id == 1).TargetResources.ShouldBe(new HashSet<int> { 2 });
            harness.Graph.RawActivities.Single(x => x.Id == 2).TargetResources.ShouldBeEmpty();
        }

        [Fact]
        public void SetWorkStreamSettings_Given_SettingsWithoutAWorkStream_Then_EveryActivityDropsItAsATarget()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [Model(1, targetWorkStreams: [1, 2]), Model(2, targetWorkStreams: [1])]);

            harness.Graph.SetWorkStreamSettings(
                new WorkStreamSettingsModel
                {
                    WorkStreams = [harness.Owner.WorkStreamSettings.WorkStreams.Single(x => x.Id == 2)],
                });

            harness.Graph.RawActivities.Single(x => x.Id == 1).TargetWorkStreams.ShouldBe(new HashSet<int> { 2 });
            harness.Graph.RawActivities.Single(x => x.Id == 2).TargetWorkStreams.ShouldBeEmpty();
        }

        [Fact]
        public void UpdateEarliestStartAndLatestFinishDateTimes_Given_ACalendarThatChanged_Then_EveryActivityCountsAgain()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [Model(1, minimumEarliestStart: s_Thursday), Model(2, minimumEarliestStart: s_Thursday)]);
            harness.Graph.RawActivities.Select(x => x.MinimumEarliestStartTime).ShouldAllBe(x => x == 3);

            // The Tuesday is a holiday from here on, and the calendar is consulted.
            harness.Calculator.SetNonWorkingDayCalendarEvents(
                [
                    new HolidayModel
                    {
                        Id = 1,
                        StartDateTime = s_Tuesday,
                        RecurrencePattern = @"FREQ=DAILY;COUNT=1",
                    },
                ]);
            harness.Calculator.NonWorkingDayMode = NonWorkingDayMode.CustomCalendar;
            harness.Graph.UpdateEarliestStartAndLatestFinishDateTimes();

            harness.Graph.RawActivities.Select(x => x.MinimumEarliestStartTime).ShouldAllBe(x => x == 2);
        }

        #endregion

        #region Display orders

        [Fact]
        public void SetDisplayOrders_Given_ActivitiesMovedInTheView_Then_EachTakesItsPlace()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var harness = new Harness();
            harness.Graph.StartBinds();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1, displayOrder: 1), Model(2, displayOrder: 2), Model(3, displayOrder: 3)]);
            main.Pump.Drain();

            harness.Graph.OrderableActivities.Move(0, 2);
            harness.Graph.SetDisplayOrders();

            harness.Graph.OrderableActivities.Select(x => x.Id).ShouldBe(new[] { 2, 3, 1 });
            harness.Graph.RawActivities.OrderBy(x => x.DisplayOrder).Select(x => x.Id).ShouldBe(new[] { 2, 3, 1 });
            harness.Graph.RawActivities.Select(x => x.DisplayOrder).OrderBy(x => x).ShouldBe(new[] { 0, 1, 2 });
        }

        #endregion

        #region Copies

        [Fact]
        public void CopyActivities_Given_Activities_Then_TheCopiesFollowTheOrderOfTheLiveGraphAndNotOfTheList()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [Model(3, displayOrder: 1), Model(1, displayOrder: 2), Model(2, displayOrder: 3)]);

            // The live graph gives the freed place of the first activity to the one that is added in its stead, and so it
            // holds them in an order that is not that of the list.
            harness.Graph.RemoveActivities([1]);
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1)]);
            harness.Graph.RawActivities.Select(x => x.Id).ShouldBe(new[] { 3, 2, 1 });

            (List<IDependentActivity> copies, Dictionary<int, IManagedActivityViewModel> sources) = harness.Graph.CopyActivities();

            copies.Select(x => x.Id).ShouldBe(new[] { 3, 1, 2 });
            sources.Keys.OrderBy(x => x).ShouldBe(new[] { 1, 2, 3 });
        }

        [Fact]
        public void CopyActivities_Given_Activities_Then_EachCopyIsIndependentAndNamesItsLiveActivity()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1, duration: 5), Model(2, duration: 6)]);

            (List<IDependentActivity> copies, Dictionary<int, IManagedActivityViewModel> sources) = harness.Graph.CopyActivities();

            copies.Count.ShouldBe(2);
            foreach (IDependentActivity copy in copies)
            {
                IManagedActivityViewModel live = harness.Graph.RawActivities.Single(x => x.Id == copy.Id);
                ReferenceEquals(sources[copy.Id], live).ShouldBeTrue();
                ReferenceEquals(copy, live).ShouldBeFalse();
                copy.Duration.ShouldBe(live.Duration);
            }

            copies[0].Duration = 99;
            harness.Graph.RawActivities.Select(x => x.Duration).OrderBy(x => x).ShouldBe(new[] { 5, 6 });
        }

        [Fact]
        public void CopySimulationActivities_Given_ActivitiesWithEstimates_Then_GivesTheCopiesTheEstimatesAndTheNames()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [
                    Model(1, name: "Alpha", duration: 10, optimistic: 4, pessimistic: 20),
                    Model(2, name: "Beta", duration: 7, dependencies: [1]),
                ]);

            // The live graph holds the activities in the order that they were added in, and the list does not: the first
            // activity goes, and is added again.
            harness.Graph.RemoveActivities([1]);
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1, name: "Alpha", duration: 10, optimistic: 4, pessimistic: 20)]);
            harness.Graph.RawActivities.Select(x => x.Id).ShouldBe(new[] { 2, 1 });

            (List<IDependentActivity<int, int, int>> activities, Dictionary<int, DurationEstimate> estimates, Dictionary<int, string> names) =
                harness.Graph.CopySimulationActivities();

            activities.Select(x => x.Id).ShouldBe(new[] { 1, 2 });
            activities.ShouldAllBe(x => !harness.Graph.RawActivities.Any(live => ReferenceEquals(live, x)));
            names.ShouldBe(new Dictionary<int, string> { [1] = "Alpha", [2] = "Beta" }, ignoreOrder: true);
            estimates[1].ShouldBe(new DurationEstimate(4, 10, 20));
            estimates[2].ShouldBe(DurationEstimate.Fixed(7));
        }

        [Fact]
        public void CopySimulationActivities_Given_AnActivityWhoseEstimatesAreOverTheLimit_Then_ItsEstimateIsItsDurationAtTheLimit()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1, duration: GraphLimits.MaximumTimeValue + 1)]);

            (_, Dictionary<int, DurationEstimate> estimates, _) = harness.Graph.CopySimulationActivities();

            estimates[1].ShouldBe(DurationEstimate.Fixed(GraphLimits.MaximumTimeValue));
        }

        #endregion

        #region Publication

        [Fact]
        public void PublishCompilation_Given_ACompilationOfTheCopies_Then_EveryLiveActivityTakesItsOwnResults()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1, duration: 3), Model(2, duration: 4, dependencies: [1])]);
            (List<IDependentActivity> copies, Dictionary<int, IManagedActivityViewModel> sources) = harness.Graph.CopyActivities();
            IGraphCompilation<int, int, int, IDependentActivity> compilation = Compile(copies);

            harness.Graph.PublishCompilation(compilation, sources);

            IManagedActivityViewModel first = harness.Graph.RawActivities.Single(x => x.Id == 1);
            IManagedActivityViewModel second = harness.Graph.RawActivities.Single(x => x.Id == 2);
            first.EarliestStartTime.ShouldBe(0);
            second.EarliestStartTime.ShouldBe(3);
            first.Successors.ShouldBe(new HashSet<int> { 2 });
            second.LatestFinishTime.ShouldBe(7);
            harness.Graph.StartTime.ShouldBe(0);
            harness.Graph.FinishTime.ShouldBe(7);
        }

        [Fact]
        public void PublishCompilation_Given_ASourceThatIsNotTheLiveActivityAnyMore_Then_ThatActivityGetsNothing()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1, duration: 3), Model(2, duration: 4, dependencies: [1])]);
            (List<IDependentActivity> copies, Dictionary<int, IManagedActivityViewModel> sources) = harness.Graph.CopyActivities();
            IGraphCompilation<int, int, int, IDependentActivity> compilation = Compile(copies);

            // The second activity goes while the compilation runs, and another takes its id.
            harness.Graph.RemoveActivities([2]);
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(2, duration: 4, dependencies: [1])]);

            harness.Graph.PublishCompilation(compilation, sources);

            harness.Graph.RawActivities.Single(x => x.Id == 1).EarliestStartTime.ShouldBe(0);
            IManagedActivityViewModel newcomer = harness.Graph.RawActivities.Single(x => x.Id == 2);
            newcomer.EarliestStartTime.ShouldBeNull();
            newcomer.LatestFinishTime.ShouldBeNull();
        }

        [Fact]
        public void PublishCompilation_Given_NoSources_Then_EveryActivityTakesItsResultsById()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1, duration: 3), Model(2, duration: 4, dependencies: [1])]);
            (List<IDependentActivity> copies, _) = harness.Graph.CopyActivities();
            IGraphCompilation<int, int, int, IDependentActivity> compilation = Compile(copies);
            harness.Graph.RemoveActivities([2]);
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(2, duration: 4, dependencies: [1])]);

            harness.Graph.PublishCompilation(compilation, sources: null);

            harness.Graph.RawActivities.Single(x => x.Id == 2).EarliestStartTime.ShouldBe(3);
        }

        #endregion

        #region Views

        [Fact]
        public void StartBinds_Given_ActivitiesAdded_Then_TheViewsFillOnTheMainThreadOnly()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var harness = new Harness();
            harness.Graph.StartBinds();

            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);

            harness.Graph.RawActivities.Count.ShouldBe(2);
            harness.Graph.Activities.ShouldBeEmpty("the view was filled on the thread that made the edit");
            harness.Graph.OrderableActivities.ShouldBeEmpty("the view was filled on the thread that made the edit");
            main.Pump.PendingCount.ShouldBeGreaterThan(0);

            main.Pump.Drain();

            harness.Graph.Activities.Select(x => x.Id).ShouldBe(new[] { 1, 2 });
            harness.Graph.OrderableActivities.Select(x => x.Id).ShouldBe(new[] { 1, 2 });
        }

        [Fact]
        public void StartBinds_Given_ABindThatWasNotStarted_Then_TheViewsStayEmpty()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var harness = new Harness();

            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);
            main.Pump.Drain();

            harness.Graph.Activities.ShouldBeEmpty();
            harness.Graph.OrderableActivities.ShouldBeEmpty();
        }

        [Fact]
        public void KillBinds_Given_StartedBinds_Then_NeitherViewTakesAnotherActivity()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var harness = new Harness();
            harness.Graph.StartBinds();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1)]);
            main.Pump.Drain();

            harness.Graph.KillBinds();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(2)]);
            main.Pump.Drain();

            harness.Graph.Activities.Select(x => x.Id).ShouldBe(new[] { 1 });
            harness.Graph.OrderableActivities.Select(x => x.Id).ShouldBe(new[] { 1 });
        }

        #endregion

        #region Links of the live graph

        // The user enters the dependencies of an activity. A compilation gives the activity resource dependencies, and the
        // publication writes them into the live activity. The tests write them there too, as the publication does, so that
        // they do not depend on what the scheduler decides. Whatever the activities hold, the live graph has to hold the
        // links of the user alone: IsIsolated reads it, and the grid shows that an activity has no dependency and no
        // successor, not that a resource puts it in a queue.

        [Fact]
        public void DependenciesString_Given_AResourceDependencyOfTheActivity_Then_NoLinkOfTheResourceIsInTheGraph()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);
            Activity(harness, 2).ResourceDependencies.Add(1);

            Activity(harness, 2).DependenciesString = string.Empty;

            Activity(harness, 1).IsIsolated.ShouldBeTrue();
            Activity(harness, 2).IsIsolated.ShouldBeTrue();
            Activity(harness, 2).ResourceDependencies.ShouldBe(new HashSet<int> { 1 }, "a compilation gave it, and an edit leaves it");
        }

        [Fact]
        public void PlanningDependenciesString_Given_AResourceDependencyOfTheActivity_Then_NoLinkOfTheResourceIsInTheGraph()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);
            Activity(harness, 2).ResourceDependencies.Add(1);

            Activity(harness, 2).PlanningDependenciesString = string.Empty;

            Activity(harness, 1).IsIsolated.ShouldBeTrue();
            Activity(harness, 2).IsIsolated.ShouldBeTrue();
            Activity(harness, 2).ResourceDependencies.ShouldBe(new HashSet<int> { 1 }, "a compilation gave it, and an edit leaves it");
        }

        [Fact]
        public void DependenciesString_Given_ADependencyThatTheResourceGaveToo_Then_TheLinkStaysUntilTheUserTakesTheDependencyAway()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);
            Activity(harness, 2).ResourceDependencies.Add(1);

            Activity(harness, 2).DependenciesString = "1";

            Activity(harness, 2).Dependencies.ShouldBe(new HashSet<int> { 1 });
            Activity(harness, 1).IsIsolated.ShouldBeFalse();
            Activity(harness, 2).IsIsolated.ShouldBeFalse();

            Activity(harness, 2).DependenciesString = string.Empty;

            Activity(harness, 2).Dependencies.ShouldBeEmpty();
            Activity(harness, 1).IsIsolated.ShouldBeTrue();
            Activity(harness, 2).IsIsolated.ShouldBeTrue();
        }

        [Fact]
        public void PlanningDependenciesString_Given_ADependencyThatTheResourceGaveToo_Then_TheLinkStaysUntilTheUserTakesTheDependencyAway()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);
            Activity(harness, 2).ResourceDependencies.Add(1);

            Activity(harness, 2).PlanningDependenciesString = "1";

            Activity(harness, 2).PlanningDependencies.ShouldBe(new HashSet<int> { 1 });
            Activity(harness, 1).IsIsolated.ShouldBeFalse();
            Activity(harness, 2).IsIsolated.ShouldBeFalse();

            Activity(harness, 2).PlanningDependenciesString = string.Empty;

            Activity(harness, 2).PlanningDependencies.ShouldBeEmpty();
            Activity(harness, 1).IsIsolated.ShouldBeTrue();
            Activity(harness, 2).IsIsolated.ShouldBeTrue();
        }

        [Fact]
        public void SetActivityDependencies_Given_AResourceDependencyOfTheActivity_Then_OnlyTheLinkOfTheUserIsInTheGraph()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2), Model(3)]);
            Activity(harness, 3).ResourceDependencies.Add(1);

            harness.Graph.SetActivityDependencies(3, [2], []);

            Activity(harness, 3).Dependencies.ShouldBe(new HashSet<int> { 2 });
            Activity(harness, 2).IsIsolated.ShouldBeFalse();
            Activity(harness, 3).IsIsolated.ShouldBeFalse();
            Activity(harness, 1).IsIsolated.ShouldBeTrue("only the resource links it to the third");
        }

        [Fact]
        public void TryApplyReduction_Given_AResourceDependencyOfAnActivityItChanges_Then_NoLinkOfTheResourceIsInTheGraph()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [Model(1), Model(2, dependencies: [1]), Model(3, dependencies: [1, 2]), Model(4)]);
            Activity(harness, 3).ResourceDependencies.Add(4);
            (_, Dictionary<int, IManagedActivityViewModel> sources) = harness.Graph.CopyActivities();
            var reduction = new TransitiveReduction(
                Compile([]),
                new Dictionary<int, ActivityDependencies>
                {
                    [1] = Dependencies(),
                    [2] = Dependencies(1),
                    [3] = Dependencies(1, 2),
                    [4] = Dependencies(),
                },
                new Dictionary<int, ActivityDependencies>
                {
                    [1] = Dependencies(),
                    [2] = Dependencies(1),
                    [3] = Dependencies(2),
                    [4] = Dependencies(),
                });

            bool applied = harness.Graph.TryApplyReduction(reduction, sources);

            applied.ShouldBeTrue();
            Activity(harness, 3).Dependencies.ShouldBe(new HashSet<int> { 2 });
            Activity(harness, 4).IsIsolated.ShouldBeTrue("only the resource links it to the third");
        }

        // Every write to the live graph is made under the leaf lock. The lock of the compiler is private to the library, and
        // it cannot keep the cleaning of the links of a resource apart from the other writes, so the leaf lock does.
        //
        // AddActivities has no test of this kind. Making an activity takes the leaf lock itself, and so the call waits for it
        // whether or not the call to the compiler is made under it. Each test below makes the call once before it holds the
        // lock: the first call of a path is slow, and a slow call must not look like a call that waits.

        [Fact]
        public void RemoveActivities_Given_TheLeafLockIsHeldByAnotherThread_Then_WaitsForIt()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2), Model(3)]);
            harness.Graph.RemoveActivities([3]);

            AssertWaitsForTheLeafLock(harness, () => harness.Graph.RemoveActivities([2]));

            harness.Graph.RawActivities.Select(x => x.Id).ShouldBe(new[] { 1 });
        }

        [Fact]
        public void ClearActivities_Given_TheLeafLockIsHeldByAnotherThread_Then_WaitsForIt()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);
            harness.Graph.ClearActivities();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);

            AssertWaitsForTheLeafLock(harness, () => harness.Graph.ClearActivities());

            harness.Graph.RawActivities.ShouldBeEmpty();
        }

        [Fact]
        public void SetActivityDependencies_Given_TheLeafLockIsHeldByAnotherThread_Then_WaitsForIt()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);
            harness.Graph.SetActivityDependencies(2, [], []);

            AssertWaitsForTheLeafLock(harness, () => harness.Graph.SetActivityDependencies(2, [1], []));

            Activity(harness, 2).Dependencies.ShouldBe(new HashSet<int> { 1 });
        }

        [Fact]
        public void TryApplyReduction_Given_TheLeafLockIsHeldByAnotherThread_Then_WaitsForIt()
        {
            (Harness harness, Dictionary<int, IManagedActivityViewModel> sources, TransitiveReduction reduction) = Reducible();
            using (harness)
            {
                harness.Graph.TryApplyReduction(reduction, sources).ShouldBeTrue();

                // The plan is not as it was copied any more, and so the call changes nothing. It takes the lock first.
                AssertWaitsForTheLeafLock(harness, () => harness.Graph.TryApplyReduction(reduction, sources).ShouldBeFalse());

                Activity(harness, 3).Dependencies.ShouldBe(new HashSet<int> { 2 });
            }
        }

        [Fact]
        public void DependenciesString_Given_TheLeafLockIsHeldByAnotherThread_Then_WaitsForIt()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);
            Activity(harness, 2).DependenciesString = string.Empty;

            AssertWaitsForTheLeafLock(harness, () => Activity(harness, 2).DependenciesString = "1");

            Activity(harness, 2).Dependencies.ShouldBe(new HashSet<int> { 1 });
        }

        [Fact]
        public void PlanningDependenciesString_Given_TheLeafLockIsHeldByAnotherThread_Then_WaitsForIt()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2)]);
            Activity(harness, 2).PlanningDependenciesString = string.Empty;

            AssertWaitsForTheLeafLock(harness, () => Activity(harness, 2).PlanningDependenciesString = "1");

            Activity(harness, 2).PlanningDependencies.ShouldBe(new HashSet<int> { 1 });
        }

        // The copies of the plan are taken in one pass while the leaf lock is held (VisitLiveActivities), so that the copy is of
        // one moment. Each activity also takes the leaf lock to copy itself (CloneObject). A call that waits for the lock does
        // not show whether the pass holds the lock, because without it the call waits at the first activity all the same.
        //
        // So these tests change the plan while the call waits. Without the lock of the pass, the call has read the plan (the
        // activities and the order of the graph) before it waits, and it finishes with that plan, or it fails because the graph
        // changed under it. With the lock, it reads the plan when the lock is free, and the copy has the change.
        //
        // No test sees a lock of the pass that ends before the last copy: nothing can change the plan between the copies of two
        // activities.

        [Fact]
        public void CopyActivities_Given_ThePlanChangesWhileTheCallWaitsForTheLeafLock_Then_TheCopyIsOfThePlanAfterTheChange()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2), Model(3)]);
            harness.Graph.CopyActivities();
            int[] copiedIds = [];

            AssertWaitsForTheLeafLock(
                harness,
                () => copiedIds = [.. harness.Graph.CopyActivities().Copies.Select(x => x.Id)],
                whileHeld: () => harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(4)]));

            copiedIds.Order().ShouldBe(new[] { 1, 2, 3, 4 });
        }

        [Fact]
        public void CopySimulationActivities_Given_ThePlanChangesWhileTheCallWaitsForTheLeafLock_Then_TheCopyIsOfThePlanAfterTheChange()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1), Model(2), Model(3)]);
            harness.Graph.CopySimulationActivities();
            int[] activityIds = [];
            int[] estimateIds = [];
            int[] nameIds = [];

            AssertWaitsForTheLeafLock(
                harness,
                () =>
                {
                    var copy = harness.Graph.CopySimulationActivities();
                    activityIds = [.. copy.Activities.Select(x => x.Id)];
                    estimateIds = [.. copy.Estimates.Keys];
                    nameIds = [.. copy.Names.Keys];
                },
                whileHeld: () => harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(4)]));

            activityIds.Order().ShouldBe(new[] { 1, 2, 3, 4 });
            estimateIds.Order().ShouldBe(new[] { 1, 2, 3, 4 });
            nameIds.Order().ShouldBe(new[] { 1, 2, 3, 4 });
        }

        // The lock of one activity, apart from the lock of the pass: a copy of the activity is never torn by an edit.
        [Fact]
        public void CloneObject_Given_TheLeafLockIsHeldByAnotherThread_Then_WaitsForIt()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(harness.Owner, s_Monday, [Model(1)]);
            Activity(harness, 1).CloneObject();
            object? copy = null;

            AssertWaitsForTheLeafLock(harness, () => copy = Activity(harness, 1).CloneObject());

            copy.ShouldBeAssignableTo<IDependentActivity>();
        }

        // Holds the leaf lock on this thread, calls on another one, and sees that the call does not finish while the lock is
        // held and does finish when it is free. The thread that holds the lock can change the plan meanwhile (whileHeld), and
        // then the call has to come back to the plan after the change.
        //
        // The call must also wait with the lock of the compiler free. The compiler calls the activity back inside some of its
        // calls (RemoveActivity calls SetAsRemovable, which takes the leaf lock), and an edit holds the leaf lock when it
        // calls the compiler. A call that took the lock of the compiler first and then waited for the leaf lock would wait
        // for the edit, and the edit would wait for it. Any other call of the compiler shows whether its lock is free, and
        // GetNextActivityId needs nothing else.
        private static void AssertWaitsForTheLeafLock(Harness harness, Action call, Action? whileHeld = null)
        {
            Task task;
            Task probe;

            lock (harness.Graph.ActivityDataLock)
            {
                task = Task.Run(call);

                Task.WaitAny([task], 300).ShouldBe(-1, "the call must wait while another thread holds the leaf lock");

                whileHeld?.Invoke();

                probe = Task.Run(() => harness.Graph.GetNextActivityId());

                Task.WaitAny([probe], 5000).ShouldBe(0, "the call waits for the leaf lock with the lock of the compiler in its hands");
            }

            task.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue("the call must finish when the leaf lock is free");
        }

        #endregion

        #region Helpers

        // Compiles the copies as a compilation does, on a compiler of its own.
        private static IGraphCompilation<int, int, int, IDependentActivity> Compile(IEnumerable<IDependentActivity> copies)
        {
            var compiler = new VertexGraphCompiler();

            foreach (IDependentActivity copy in copies)
            {
                compiler.AddActivity(copy);
            }

            return compiler.Compile(CancellationToken.None);
        }

        private static DependentActivityModel Model(
            int id,
            int displayOrder = 0,
            string name = "",
            int duration = 1,
            int? optimistic = null,
            int? pessimistic = null,
            List<int>? dependencies = null,
            DateTimeOffset? minimumEarliestStart = null,
            List<int>? targetResources = null,
            List<int>? targetWorkStreams = null) =>
            new()
            {
                Activity = new ActivityModel
                {
                    Id = id,
                    DisplayOrder = displayOrder,
                    Name = name,
                    Duration = duration,
                    MostLikelyDuration = duration,
                    OptimisticDuration = optimistic,
                    PessimisticDuration = pessimistic,
                    MinimumEarliestStartDateTime = minimumEarliestStart,
                    TargetResources = targetResources ?? [],
                    TargetWorkStreams = targetWorkStreams ?? [],
                },
                Dependencies = dependencies ?? [],
            };

        private static DateTimeOffset Local(int year, int month, int day) =>
            new(
                new DateTime(year, month, day),
                TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, month, day)));

        /// <summary>
        /// A graph, with the core that owns its activities, two resources and two work streams.
        /// </summary>
        private sealed class Harness
            : IDisposable
        {
            public Harness()
            {
                CoreViewModelFixture.EnsureReactiveUIInitialized();

                Owner = CoreViewModelFixture.Create();
                Owner.ProcessProjectScenario(
                    CoreViewModelFixture.CreateProjectScenario(0) with
                    {
                        ProjectStart = s_Monday,
                        Today = s_Monday,
                        WorkStreamSettings = new WorkStreamSettingsModel
                        {
                            WorkStreams =
                            [
                                new WorkStreamModel { Id = 1, Name = "Work stream 1", DisplayOrder = 1 },
                                new WorkStreamModel { Id = 2, Name = "Work stream 2", DisplayOrder = 2 },
                            ],
                        },
                    },
                    Guid.NewGuid(),
                    @"Test");

                Calculator = new DateTimeCalculator(TimeProvider.System);
                Calculator.ProjectStart = s_Monday;
                Graph = new PlanGraph(new ProjectPlanMapper(), Calculator, new SubscriptionLifetime());
            }

            public CoreViewModel Owner { get; }

            public DateTimeCalculator Calculator { get; }

            public PlanGraph Graph { get; }

            public void Dispose()
            {
                Graph.ClearActivities();
                Graph.Dispose();
                Owner.Dispose();
            }
        }

        #endregion
    }
}
