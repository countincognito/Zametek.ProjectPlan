using Avalonia.Threading;
using Shouldly;
using System.Collections.Specialized;
using System.ComponentModel;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan.Tests
{
    /// <summary>
    /// The selectors of activities follow the plan, and the tracking tabs follow the selectors, the window of days and the
    /// compilations. These tests work the real application on the plan of the fixture, with automatic compilation on and off.
    /// The plan has bookings of activity 2 by resources 13, 14 and 15 on days 0 to 4, a booking of activity 1 by resource 2 on
    /// day 0, and progress on activity 2 on days 0 to 4.
    /// </summary>
    [Collection(ApplicationCollection.Name)]
    public class TrackerRevisionTests
    {
        private const int c_Activities = 46;
        private const int c_ActivityWithProgress = 2;
        private const int c_ResourceOfThatActivity = 13;
        private const int c_ActivityBookedOnce = 1;
        private const int c_ResourceOfTheOtherActivity = 2;

        private readonly ApplicationFixture m_Fixture;

        public TrackerRevisionTests(ApplicationFixture fixture)
        {
            m_Fixture = fixture;
        }

        private Task RunAsync(bool autoCompile, Func<ApplicationDriver, Task> test) =>
            m_Fixture.RunAsync(async app =>
            {
                bool before = app.Main.AutoCompile;
                app.Main.AutoCompile = autoCompile;

                try
                {
                    await test(app);
                }
                finally
                {
                    // The application stays for the next test.
                    app.Main.AutoCompile = before;
                    app.Core.TrackerIndex = 0;
                    await app.SettleAsync();
                }
            });

        private static IResourceTimesheetViewModel SectionOf(ApplicationDriver app, int resourceId) =>
            app.Effort.TimesheetSections.Single(x => x.ResourceId == resourceId);

        private static void Rename(ApplicationDriver app, int activityId, string name)
        {
            IManagedActivityViewModel activity = app.Core.RawActivities.Single(x => x.Id == activityId);
            var editable = (IEditableObject)activity;
            editable.BeginEdit();
            activity.Name = name;
            editable.EndEdit();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task AddManagedActivityCommand_Given_ABookedDay_Then_TheActivityReachesTheSelectorsAndTheTimesheet(bool autoCompile) =>
            RunAsync(autoCompile, async app =>
            {
                app.Activities.AddManagedActivityCommand.Execute(null);
                await app.UntilAsync(() => app.Core.RawActivities.Count == c_Activities + 1, @"the activity added");
                await app.SelectorsShouldFollowThePlanAsync(c_ResourceOfThatActivity);

                int added = app.Core.RawActivities.Max(x => x.Id);
                await app.UntilAsync(
                    () => SectionOf(app, c_ResourceOfThatActivity).CandidateActivities.Any(x => x.Id == added),
                    @"the new activity offered in the timesheet");
            });

        [Fact]
        public Task RemoveManagedActivitiesCommand_Given_ABooking_Then_TheBookingAndItsRowGo() =>
            RunAsync(true, async app =>
            {
                await app.UntilAsync(() => SectionOf(app, c_ResourceOfTheOtherActivity).Rows.Count == 1, @"the row of the booking");

                await app.RemoveActivityAsync(c_ActivityBookedOnce);

                await app.UntilAsync(() => SectionOf(app, c_ResourceOfTheOtherActivity).Rows.Count == 0, @"the row of the booking gone");
                app.Resource(c_ResourceOfTheOtherActivity).TrackerSet.CloneTrackers().ShouldBeEmpty();
                await app.SelectorsShouldFollowThePlanAsync(c_ResourceOfThatActivity);
            });

        [Fact]
        public Task RemoveManagedActivitiesCommand_Given_NoCompilation_Then_TheBookingGoes() =>
            RunAsync(false, async app =>
            {
                await app.RemoveActivityAsync(c_ActivityBookedOnce);

                await app.UntilAsync(
                    () => app.Resource(c_ResourceOfTheOtherActivity).TrackerSet.GetDay(0).SelectedResourceActivityIds.Count == 0,
                    @"the booking gone");
                await app.SelectorsShouldFollowThePlanAsync(c_ResourceOfThatActivity);
            });

        [Fact]
        public Task ProgressGrid_Given_ARemovalWithNoCompilation_Then_ItsCellsFollowTheWindow() =>
            RunAsync(false, async app =>
            {
                // The remove command asks for a revision, and nothing compiles. The titles of the days then moved with the window,
                // and the values in the cells stayed where they were, until the next compilation.
                await app.ShowTabAsync(app.Progress);
                await app.UntilAsync(() => app.ProgressCellsOf(c_ActivityWithProgress).FirstOrDefault() == @"5%", @"the first day of the progress on show");

                await app.RemoveActivityAsync(c_ActivityBookedOnce);
                app.Core.TrackerIndex = 1;

                await app.UntilAsync(() => app.ProgressCellsOf(c_ActivityWithProgress).FirstOrDefault() == @"10%", @"the cells at the next day");
                app.ProgressCellsOf(c_ActivityWithProgress).ShouldBe(new[] { @"10%", @"15%", @"20%", @"25%" });
            });

        [Fact]
        public Task Grid_Given_ARename_Then_TheSelectorsAndTheTimesheetShowTheNewName() =>
            RunAsync(true, async app =>
            {
                Rename(app, c_ActivityWithProgress, @"Renamed");

                await app.UntilAsync(
                    () => app.Resource(c_ResourceOfThatActivity).TrackerSet.GetDay(0).TargetResourceActivities.Single(x => x.Id == c_ActivityWithProgress).Name == @"Renamed",
                    @"the new name in the day of the resource");
                await app.UntilAsync(
                    () => app.Gantt.ActivitySelector.TargetActivities.Single(x => x.Id == c_ActivityWithProgress).Name == @"Renamed",
                    @"the new name in the selector of the Gantt chart");
                await app.UntilAsync(
                    () => SectionOf(app, c_ResourceOfThatActivity).Rows.Single(x => x.ActivityId == c_ActivityWithProgress).ActivityName == @"Renamed",
                    @"the new name in the timesheet");
            });

        [Fact]
        public Task CompileCommand_Given_NoAutomaticCompilation_Then_TheTrackersAreRevisedBeforeTheCompilation() =>
            RunAsync(false, async app =>
            {
                var raises = new List<string>();
                PropertyChangedEventHandler record = (_, e) =>
                {
                    if (e.PropertyName is nameof(ICoreViewModel.TrackersRevision) or nameof(ICoreViewModel.GraphCompilation))
                    {
                        lock (raises)
                        {
                            raises.Add(e.PropertyName);
                        }
                    }
                };
                var core = (INotifyPropertyChanged)app.Core;
                core.PropertyChanged += record;

                try
                {
                    Rename(app, c_ActivityWithProgress, @"Renamed");
                    await app.SettleAsync();
                    app.Resource(c_ResourceOfThatActivity).TrackerSet.GetDay(0).TargetResourceActivities.Single(x => x.Id == c_ActivityWithProgress).Name
                        .ShouldNotBe(@"Renamed", "with no compilation, the names wait for the compile command");

                    app.Main.CompileCommand.Execute(null);
                    await app.UntilAsync(
                        () => { lock (raises) { return raises.Contains(nameof(ICoreViewModel.GraphCompilation)); } },
                        @"the compilation published");

                    lock (raises)
                    {
                        raises[0].ShouldBe(nameof(ICoreViewModel.TrackersRevision));
                    }

                    app.Resource(c_ResourceOfThatActivity).TrackerSet.GetDay(0).TargetResourceActivities.Single(x => x.Id == c_ActivityWithProgress).Name
                        .ShouldBe(@"Renamed");
                    await app.UntilAsync(
                        () => SectionOf(app, c_ResourceOfThatActivity).Rows.Single(x => x.ActivityId == c_ActivityWithProgress).ActivityName == @"Renamed",
                        @"the new name in the timesheet");
                }
                finally
                {
                    core.PropertyChanged -= record;
                }
            });

        [Fact]
        public Task Commands_Given_TheDropdownOfTheGanttChartOpen_Then_ItsListsChangeOnTheUIThread() =>
            RunAsync(true, async app =>
            {
                // The commands remove and add an activity on threads of their own, and each asks for a revision there. The open
                // dropdown shows the lists of the selector, and a bound list can change only on the UI thread.
                await app.ShowTabAsync(app.Gantt);
                await app.ClickItemsAsync(app.Gantt, c_ActivityBookedOnce);
                app.Gantt.ActivitySelector.SelectedActivityIds.ShouldContain(c_ActivityBookedOnce, "precondition: the filter has the activity");

                var offTheUIThread = new List<NotifyCollectionChangedAction>();
                void Record(object? sender, NotifyCollectionChangedEventArgs e)
                {
                    if (!Dispatcher.UIThread.CheckAccess())
                    {
                        lock (offTheUIThread)
                        {
                            offTheUIThread.Add(e.Action);
                        }
                    }
                }

                var targets = (INotifyCollectionChanged)app.Gantt.ActivitySelector.TargetActivities;
                targets.CollectionChanged += Record;
                app.Gantt.ActivitySelector.SelectedTargetActivities.CollectionChanged += Record;
                await app.OpenDropDownAsync(app.Gantt);

                try
                {
                    await app.RemoveActivityAsync(c_ActivityBookedOnce);
                    await app.SelectorsShouldFollowThePlanAsync(c_ResourceOfThatActivity);
                    app.Activities.AddManagedActivityCommand.Execute(null);
                    await app.UntilAsync(() => app.Core.RawActivities.Count == c_Activities, @"an activity added");
                    await app.SelectorsShouldFollowThePlanAsync(c_ResourceOfThatActivity);

                    app.Gantt.ActivitySelector.SelectedActivityIds.ShouldNotContain(c_ActivityBookedOnce);
                    app.ItemCountOfTheDropdown(app.Gantt).ShouldBe(c_Activities, @"the open dropdown does not show the plan");
                }
                finally
                {
                    targets.CollectionChanged -= Record;
                    app.Gantt.ActivitySelector.SelectedTargetActivities.CollectionChanged -= Record;
                    await app.CloseDropDownAsync(app.Gantt);
                }

                lock (offTheUIThread)
                {
                    offTheUIThread.ShouldBeEmpty(@"a list of the selector of the Gantt chart changed on a thread that is not the UI thread");
                }
            });

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task RemoveManagedActivitiesCommand_Given_AnActivityInTheFilterOfTheGanttChart_Then_TheBoxLetsItGo(bool autoCompile) =>
            RunAsync(autoCompile, async app =>
            {
                // With its dropdown closed, the box of the filter shows the selected activities. The command takes one of them away
                // on a thread of its own.
                await app.ShowTabAsync(app.Gantt);
                await app.ClickItemsAsync(app.Gantt, c_ActivityBookedOnce);
                app.TextsOfTheBox(app.Gantt).ShouldContain(x => x.StartsWith($@"{c_ActivityBookedOnce} - ", StringComparison.Ordinal), @"precondition: the box shows the activity");

                await app.RemoveActivityAsync(c_ActivityBookedOnce);
                await app.SelectorsShouldFollowThePlanAsync(c_ResourceOfThatActivity);

                app.ShouldHaveNoErrors();
                app.Gantt.ActivitySelector.SelectedActivityIds.ShouldNotContain(c_ActivityBookedOnce);
                app.TextsOfTheBox(app.Gantt).ShouldNotContain(x => x.StartsWith($@"{c_ActivityBookedOnce} - ", StringComparison.Ordinal), @"the box still shows the activity");
            });

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task InsertManagedActivityCommand_Given_ABooking_Then_TheBookingStaysWithItsActivity(bool autoCompile) =>
            RunAsync(autoCompile, async app =>
            {
                string? name = app.Core.RawActivities.Single(x => x.Id == c_ActivityWithProgress).Name;

                app.SelectActivity(c_ActivityWithProgress);
                app.Activities.InsertManagedActivityCommand.Execute(null);
                await app.UntilAsync(() => app.Core.RawActivities.Count == c_Activities + 1, @"the activity inserted");
                await app.SelectorsShouldFollowThePlanAsync(c_ResourceOfThatActivity);

                IResourceActivitySelectorViewModel day = app.Resource(c_ResourceOfThatActivity).TrackerSet.GetDay(0);
                day.SelectedTargetResourceActivities.ShouldHaveSingleItem().Name.ShouldBe(name);
            });

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task RenumberCommands_Given_Bookings_Then_TheBookingsStay(bool autoCompile) =>
            RunAsync(autoCompile, async app =>
            {
                int bookingsBefore = app.Resources.RawResources.Sum(x => x.TrackerSet.CloneTrackers().Count);

                app.Activities.RenumberActivitiesCommand.Execute(null);
                await app.SettleAsync();
                app.Resources.RenumberResourcesCommand.Execute(null);
                await app.SettleAsync();

                await app.SelectorsShouldFollowThePlanAsync(c_ResourceOfThatActivity);
                app.Resources.RawResources.Sum(x => x.TrackerSet.CloneTrackers().Count).ShouldBe(bookingsBefore);
            });

        [Fact]
        public Task LoadAsync_Given_Bookings_Then_EachScenarioKeepsThemAndTheSelectorsFollowThePlan() =>
            RunAsync(true, async app =>
            {
                IManagedNodeViewModel first = app.Scenarios[0];
                IManagedNodeViewModel second = await app.DuplicateAsync(first);
                int bookings = app.SavedScenario(first).ResourceSettings.Resources.Sum(x => x.Trackers.Count);
                bookings.ShouldBeGreaterThan(0, "precondition: the plan has bookings");

                for (int i = 0; i < 3; i++)
                {
                    await app.LoadAsync(second);
                    await app.SelectorsShouldFollowThePlanAsync(c_ResourceOfThatActivity);
                    await app.UntilAsync(
                        () => SectionOf(app, c_ResourceOfThatActivity).Rows.Any(x => x.ActivityId == c_ActivityWithProgress),
                        @"the row of the booking after the load");

                    await app.LoadAsync(first);
                    await app.SelectorsShouldFollowThePlanAsync(c_ResourceOfThatActivity);
                }

                app.Core.IsProjectScenarioUpdated.ShouldBeFalse(@"nothing has changed");
                ProjectScenarioModel savedFirst = app.SavedScenario(first);
                ProjectScenarioModel savedSecond = app.SavedScenario(second);
                savedFirst.ResourceSettings.Resources.Sum(x => x.Trackers.Count).ShouldBe(bookings);
                savedSecond.ResourceSettings.Resources.Sum(x => x.Trackers.Count).ShouldBe(bookings);
            });
    }
}
