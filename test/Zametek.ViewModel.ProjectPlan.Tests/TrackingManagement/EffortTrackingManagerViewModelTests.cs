using Shouldly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The timesheet follows the window of days, the compilations, and the lists of resources and activities. A request for a
    /// revision of the trackers does not rebuild it. It used to rebuild for the request and then for the compilation after it,
    /// on the UI thread, and a compilation with no request before it did not rebuild it at all. A row and a candidate show an
    /// activity as the Show Connections list of the Gantt chart does (ActivityLabelHelper).
    /// </summary>
    public class EffortTrackingManagerViewModelTests
    {
        private const int c_ActivityCount = 6;
        private const int c_ResourceId = 1;
        private const int c_BookedActivityId = 2;

        private static ProjectScenarioModel PlanWithABooking()
        {
            ProjectScenarioModel plan = CoreViewModelFixture.CreateProjectScenario(c_ActivityCount);

            List<ResourceModel> resources = [.. plan.ResourceSettings.Resources.Select(resource => resource.Id != c_ResourceId
                ? resource
                : resource with
                {
                    Trackers =
                    [
                        new ResourceTrackerModel
                        {
                            Time = 1,
                            ResourceId = c_ResourceId,
                            ActivityTrackers =
                            [
                                new ResourceActivityTrackerModel { Time = 1, ResourceId = c_ResourceId, ActivityId = c_BookedActivityId, ActivityName = @"Activity 2", PercentageWorked = 50 },
                            ],
                        },
                    ],
                })];

            return plan with { ResourceSettings = plan.ResourceSettings with { Resources = resources } };
        }

        private sealed class Harness
            : IDisposable
        {
            private readonly MainThreadSequencerScope m_Main;
            private readonly TaskpoolSequencerScope m_Pool;
            private readonly CoreViewModelFixture.SubscribedHarness m_Subscribed;
            private readonly EffortTrackingManagerViewModel m_Manager;

            public Harness()
            {
                CoreViewModelFixture.EnsureReactiveUIInitialized();
                m_Main = new MainThreadSequencerScope();
                m_Pool = new TaskpoolSequencerScope();
                m_Subscribed = CoreViewModelFixture.CreateWithSubscriptions();
                m_Manager = new EffortTrackingManagerViewModel(
                    m_Subscribed.Core,
                    m_Subscribed.ResourceSettingsManager,
                    new DateTimeCalculator(TimeProvider.System));
                m_Manager.StartSubscriptions();
                Settle();

                Core.ProcessProjectScenario(PlanWithABooking(), Guid.NewGuid(), @"Test");
                Settle();

                ((INotifyPropertyChanged)Section).PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(IResourceTimesheetViewModel.ResourceName))
                    {
                        Rebuilds++;
                    }
                };
            }

            public CoreViewModel Core => m_Subscribed.Core;

            public IResourceTimesheetViewModel Section => m_Manager.TimesheetSections.Single(x => x.ResourceId == c_ResourceId);

            // A rebuild raises the name of the resource of each section, as the last thing it does.
            public int Rebuilds { get; private set; }

            // The bookings of the resource as a save copies them: the percentage for each day and activity.
            public Dictionary<(int Day, int ActivityId), int> Bookings() =>
                m_Subscribed.ResourceSettingsManager.RawResources.Single(x => x.Id == c_ResourceId).TrackerSet.CloneTrackers()
                    .SelectMany(tracker => tracker.ActivityTrackers)
                    .ToDictionary(x => (x.Time, x.ActivityId), x => x.PercentageWorked);

            public void Settle() => Drain(m_Main, m_Pool);

            public void Dispose()
            {
                m_Manager.Dispose();
                m_Subscribed.Dispose();
                m_Pool.Dispose();
                m_Main.Dispose();
            }
        }

        [Fact]
        public void RunCompile_Given_ARequestBeforeIt_Then_TheTimesheetRebuildsOnce()
        {
            using var harness = new Harness();

            harness.Core.ReviseTrackers();
            harness.Core.RunCompile();
            harness.Settle();

            harness.Rebuilds.ShouldBe(1);
        }

        [Fact]
        public void RunCompile_Given_NoRequestBeforeIt_Then_TheTimesheetRebuilds()
        {
            using var harness = new Harness();

            harness.Core.RunCompile();
            harness.Settle();

            harness.Rebuilds.ShouldBe(1);
        }

        [Fact]
        public void ReviseTrackers_Given_NoCompilation_Then_TheTimesheetDoesNotRebuild()
        {
            using var harness = new Harness();

            harness.Core.ReviseTrackers();
            harness.Settle();

            harness.Rebuilds.ShouldBe(0);
        }

        [Fact]
        public void RunCompile_Given_ARename_Then_TheRowShowsTheNewName()
        {
            using var harness = new Harness();
            harness.Section.Rows.ShouldHaveSingleItem().ActivityName.ShouldBe(@"Activity 2", "precondition: the booking has a row");

            harness.Core.UpdateManagedActivities([new UpdateDependentActivityModel { Id = c_BookedActivityId, IsNameEdited = true, Name = @"Renamed" }]);
            harness.Core.RunCompile();
            harness.Settle();

            IResourceTimesheetRowViewModel row = harness.Section.Rows.ShouldHaveSingleItem();
            row.ActivityName.ShouldBe(@"Renamed");
            row.ActivityLabel.ShouldBe($@"{c_BookedActivityId} - Renamed");
        }

        [Fact]
        public void RunCompile_Given_ARenameToNoName_Then_TheLabelOfTheRowShowsTheNumberTwice()
        {
            using var harness = new Harness();
            harness.Section.Rows.ShouldHaveSingleItem().ActivityName.ShouldBe(@"Activity 2", "precondition: the booking has a row");

            harness.Core.UpdateManagedActivities([new UpdateDependentActivityModel { Id = c_BookedActivityId, IsNameEdited = true, Name = string.Empty }]);
            harness.Core.RunCompile();
            harness.Settle();

            IResourceTimesheetRowViewModel row = harness.Section.Rows.ShouldHaveSingleItem();
            row.ActivityName.ShouldBeEmpty("the cell of the name shows the name, and the column of the number is next to it");
            row.ActivityLabel.ShouldBe($@"{c_BookedActivityId} - {c_BookedActivityId}", "the Show Connections list of the Gantt chart shows an activity with no name in this way");
        }

        [Fact]
        public void AddManagedActivity_Given_NoName_Then_ItsCandidateShowsTheNumberTwice()
        {
            using var harness = new Harness();

            int added = harness.Core.AddManagedActivity(c_ActivityCount + 1);
            harness.Core.ReviseTrackers();
            harness.Settle();

            harness.Section.CandidateActivities.Single(x => x.Id == added).DisplayName.ShouldBe($@"{added} - {added}");
        }

        [Fact]
        public void ReviseTrackers_Given_AnActivityWithAName_Then_ItsCandidateShowsTheNumberAndTheName()
        {
            using var harness = new Harness();

            harness.Core.ReviseTrackers();
            harness.Settle();

            harness.Section.CandidateActivities.Single(x => x.Id == 1).DisplayName.ShouldBe(@"1 - Activity 1");
        }

        [Fact]
        public void TrackerIndex_Given_ARequestWithNoCompilation_Then_TheTimesheetFollowsTheWindow()
        {
            using var harness = new Harness();
            harness.Core.ReviseTrackers();
            harness.Settle();

            harness.Core.TrackerIndex = 3;
            harness.Settle();

            harness.Rebuilds.ShouldBe(1);
        }

        [Fact]
        public void AddManagedActivity_Given_NoCompilation_Then_ItIsACandidateOfTheTimesheet()
        {
            using var harness = new Harness();

            int added = harness.Core.AddManagedActivity(c_ActivityCount + 1);
            harness.Core.ReviseTrackers();
            harness.Settle();

            harness.Section.CandidateActivities.Select(x => x.Id).ShouldContain(added);
        }

        [Fact]
        public void RemoveManagedActivities_Given_ABookingAndNoCompilation_Then_TheRowGoes()
        {
            using var harness = new Harness();
            harness.Section.Rows.ShouldHaveSingleItem();

            harness.Core.RemoveManagedActivities([c_BookedActivityId]);
            harness.Core.ReviseTrackers();
            harness.Settle();

            harness.Section.Rows.ShouldBeEmpty("the row of a booking of an activity that the plan no longer has stayed");
        }

        [Fact]
        public void PercentageWorked_Given_AnEditOfAnEmptyCell_Then_TheResourceBooksTheActivity()
        {
            using var harness = new Harness();
            ITimesheetCellViewModel cell = harness.Section.Rows.ShouldHaveSingleItem().Cells[0];
            cell.PercentageWorked.ShouldBeNull("precondition: the booking is on the day after");

            cell.PercentageWorked = 30;
            harness.Settle();

            cell.PercentageWorked.ShouldBe(30);
            harness.Bookings().ShouldBe(new Dictionary<(int Day, int ActivityId), int> { [(0, c_BookedActivityId)] = 30, [(1, c_BookedActivityId)] = 50 });
        }

        [Fact]
        public void PercentageWorked_Given_AClearedCell_Then_TheBookingGoes()
        {
            using var harness = new Harness();
            ITimesheetCellViewModel cell = harness.Section.Rows.ShouldHaveSingleItem().Cells[1];
            cell.PercentageWorked.ShouldBe(50, "precondition: the cell shows the booking");

            cell.PercentageWorked = null;
            harness.Settle();

            harness.Bookings().ShouldBeEmpty();
        }
    }
}
