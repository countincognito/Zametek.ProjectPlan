using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        [Fact]
        public void TransitiveReduction_Given_ADependencyThatAnotherImplies_Then_ItIsRemoved()
        {
            using var harness = new Harness();
            harness.Graph.AddActivities(
                harness.Owner,
                s_Monday,
                [Model(1), Model(2, dependencies: [1]), Model(3, dependencies: [1, 2])]);

            harness.Graph.TransitiveReduction();

            harness.Graph.RawActivities.Single(x => x.Id == 3).Dependencies.ShouldBe(new HashSet<int> { 2 });
            harness.Graph.RawActivities.Single(x => x.Id == 2).Dependencies.ShouldBe(new HashSet<int> { 1 });
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
