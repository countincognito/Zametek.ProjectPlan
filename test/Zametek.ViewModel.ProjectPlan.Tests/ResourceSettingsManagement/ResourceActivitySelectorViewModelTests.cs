using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A day of a resource keeps its lists to itself, and it gives each reader an array that a change replaces as a whole. A
    /// revision of the trackers changes a day on the thread that asks for it, and the cells of the timesheet and a copy of the
    /// trackers read the day on other threads. A read used to meet the change and throw. An edit in a cell changes the day under
    /// the lock of the day, as a revision does.
    /// </summary>
    public class ResourceActivitySelectorViewModelTests
    {
        private static readonly TimeSpan s_Patience = TimeSpan.FromSeconds(30);

        private const int c_ActivityCount = 6;
        private const int c_BookedActivityId = 2;
        private const int c_FreeActivityId = 3;

        private static CoreViewModel CreateLoaded()
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            return core;
        }

        // One day of a resource, with one booking of half a day.
        private static ResourceActivitySelectorViewModel BookedDay(ICoreViewModel core) =>
            new(core, new ResourceTrackerModel
            {
                Time = 0,
                ResourceId = 1,
                ActivityTrackers =
                [
                    new ResourceActivityTrackerModel
                    {
                        Time = 0,
                        ResourceId = 1,
                        ActivityId = c_BookedActivityId,
                        ActivityName = $@"Activity {c_BookedActivityId}",
                        PercentageWorked = 50,
                    },
                ],
            });

        private static Dictionary<int, int> Bookings(IResourceActivitySelectorViewModel day) =>
            day.SelectedTargetResourceActivities.ToDictionary(x => x.Id, x => x.PercentageWorked);

        #region A read that meets a revision

        [Fact]
        public void TargetResourceActivities_Given_ARevisionDuringARead_Then_TheReadCarriesOn()
        {
            // The UI thread reads a day while a revision on another thread changes it. Here the revision lands in the middle of the
            // read on one thread, and so it does each time.
            using CoreViewModel core = CreateLoaded();
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            using IEnumerator<ISelectableResourceActivityViewModel> read = day.TargetResourceActivities.GetEnumerator();
            read.MoveNext().ShouldBeTrue("precondition: the day has targets");

            int added = core.AddManagedActivity(c_ActivityCount + 1);
            core.ReviseTrackers();

            int count = 1;
            Should.NotThrow(() =>
            {
                while (read.MoveNext())
                {
                    count++;
                }
            }, "a read of the targets met a revision");
            count.ShouldBe(c_ActivityCount, "the read saw a part of the revision");
            day.TargetResourceActivities.Select(x => x.Id).ShouldContain(added);
        }

        [Fact]
        public void SelectedTargetResourceActivities_Given_ARevisionDuringARead_Then_TheReadCarriesOn()
        {
            using CoreViewModel core = CreateLoaded();
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            using IEnumerator<ISelectableResourceActivityViewModel> read = day.SelectedTargetResourceActivities.GetEnumerator();
            read.MoveNext().ShouldBeTrue("precondition: the day has a booking");

            core.RemoveManagedActivities([c_BookedActivityId]);
            core.ReviseTrackers();

            Should.NotThrow(() => read.MoveNext(), "a read of the bookings met a revision");
            day.SelectedTargetResourceActivities.ShouldBeEmpty();
            day.SelectedResourceActivityIds.ShouldBeEmpty();
        }

        [Fact]
        public void ReviseTrackers_Given_NothingNew_Then_TheReadersKeepTheSameArrays()
        {
            // A plan with bookings has a day for each booked day of each resource, and a revision with nothing new must not make
            // new arrays for all of them.
            using CoreViewModel core = CreateLoaded();
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            IReadOnlyList<ISelectableResourceActivityViewModel> targets = day.TargetResourceActivities;
            IReadOnlyList<ISelectableResourceActivityViewModel> selected = day.SelectedTargetResourceActivities;
            IList<int> ids = day.SelectedResourceActivityIds;

            core.ReviseTrackers();
            core.ReviseTrackers();

            day.TargetResourceActivities.ShouldBeSameAs(targets);
            day.SelectedTargetResourceActivities.ShouldBeSameAs(selected);
            day.SelectedResourceActivityIds.ShouldBeSameAs(ids);
            Bookings(day).ShouldBe(new Dictionary<int, int> { [c_BookedActivityId] = 50 });
        }

        #endregion

        #region An edit in a cell

        [Fact]
        public void SetPercentageWorked_Given_AnActivityWithNoBooking_Then_TheDayBooksIt()
        {
            using CoreViewModel core = CreateLoaded();
            using ResourceActivitySelectorViewModel day = BookedDay(core);

            day.SetPercentageWorked(c_FreeActivityId, 30).ShouldBeTrue();

            Bookings(day).ShouldBe(new Dictionary<int, int> { [c_BookedActivityId] = 50, [c_FreeActivityId] = 30 });
            day.SelectedResourceActivityIds.Order().ToArray().ShouldBe(new[] { c_BookedActivityId, c_FreeActivityId });
        }

        [Fact]
        public void SetPercentageWorked_Given_ABooking_Then_ItsValueChanges()
        {
            using CoreViewModel core = CreateLoaded();
            using ResourceActivitySelectorViewModel day = BookedDay(core);

            day.SetPercentageWorked(c_BookedActivityId, 75).ShouldBeTrue();

            Bookings(day).ShouldBe(new Dictionary<int, int> { [c_BookedActivityId] = 75 });
        }

        [Fact]
        public void SetPercentageWorked_Given_NoValue_Then_TheBookingGoes()
        {
            using CoreViewModel core = CreateLoaded();
            using ResourceActivitySelectorViewModel day = BookedDay(core);

            day.SetPercentageWorked(c_BookedActivityId, null).ShouldBeTrue();

            day.SelectedTargetResourceActivities.ShouldBeEmpty();
            day.SelectedResourceActivityIds.ShouldBeEmpty();
            day.TargetResourceActivities.Select(x => x.Id).ShouldContain(c_BookedActivityId, "the activity stays a target of the day");
        }

        [Fact]
        public void SetPercentageWorked_Given_AnActivityThatTheDayDoesNotHave_Then_NothingChanges()
        {
            using CoreViewModel core = CreateLoaded();
            using ResourceActivitySelectorViewModel day = BookedDay(core);

            day.SetPercentageWorked(c_ActivityCount + 1, 30).ShouldBeFalse();

            Bookings(day).ShouldBe(new Dictionary<int, int> { [c_BookedActivityId] = 50 });
        }

        [Fact]
        public void SetPercentageWorked_Given_TheEmptyDay_Then_NothingChanges()
        {
            ResourceActivitySelectorViewModel.Empty.SetPercentageWorked(c_BookedActivityId, 30).ShouldBeFalse();

            ResourceActivitySelectorViewModel.Empty.SelectedTargetResourceActivities.ShouldBeEmpty();
        }

        [Fact]
        public async Task SetPercentageWorked_Given_RevisionsOnAnotherThread_Then_EachEditStays()
        {
            // The UI thread edits cells while commands add and remove activities on threads of their own. A revision reads the
            // bookings and writes them back, and an edit between that read and that write was lost.
            using CoreViewModel core = CreateLoaded();
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            using var stop = new CancellationTokenSource();
            int revisions = 0;

            Task commands = Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                {
                    int added = core.AddManagedActivity(c_ActivityCount + 1);
                    core.ReviseTrackers();
                    core.RemoveManagedActivities([added]);
                    core.ReviseTrackers();
                    Interlocked.Increment(ref revisions);
                }
            });

            try
            {
                DateTime limit = DateTime.UtcNow.Add(s_Patience);
                int edits = 0;

                // At least this many of each, so that many revisions land between edits. A read of the bookings by a revision just
                // outside its lock leaves a window of a few instructions, and with 200 pairs this test met it in 1 of 5 runs.
                while (Volatile.Read(ref revisions) < 1_000 || edits < 10_000)
                {
                    DateTime.UtcNow.ShouldBeLessThan(limit, $@"the test made {edits} edits and {Volatile.Read(ref revisions)} pairs of revisions in its time");
                    edits++;
                    int? value = edits % 2 == 0 ? null : edits % 100;

                    day.SetPercentageWorked(c_FreeActivityId, value).ShouldBeTrue();

                    int? stayed = day.SelectedTargetResourceActivities.FirstOrDefault(x => x.Id == c_FreeActivityId)?.PercentageWorked;
                    stayed.ShouldBe(value, $@"edit {edits} did not stay");
                }

                day.SetPercentageWorked(c_FreeActivityId, 77).ShouldBeTrue();
            }
            finally
            {
                stop.Cancel();
            }

            await commands.WaitAsync(s_Patience);

            Bookings(day).ShouldBe(new Dictionary<int, int> { [c_BookedActivityId] = 50, [c_FreeActivityId] = 77 });
        }

        #endregion
    }
}
