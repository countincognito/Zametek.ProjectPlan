using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A command of the activities manager that changes the list of activities asks for a revision of the trackers, with or
    /// without automatic compilation. A selector of activities then shows the new list.
    /// </summary>
    public class ActivitiesManagerTrackerRevisionTests
    {
        private const int c_ActivityCount = 6;
        private const int c_BookedActivityId = 2;

        private static async Task WaitUntilAsync(Func<bool> condition, string what)
        {
            DateTime limit = DateTime.UtcNow.AddSeconds(10);

            while (!condition())
            {
                DateTime.UtcNow.ShouldBeLessThan(limit, what);
                await Task.Delay(5);
            }
        }

        private sealed class Harness
            : IDisposable
        {
            public Harness(bool autoCompile)
            {
                Core = CoreViewModelFixture.Create();
                Core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
                Core.AutoCompile = autoCompile;
                Manager = new ActivitiesManagerViewModel(Core, CoreViewModelFixture.CreateDialogService());
                Gantt = new GanttActivitySelectorViewModel(Core);
                Day = new ResourceActivitySelectorViewModel(Core, new ResourceTrackerModel
                {
                    Time = 0,
                    ResourceId = 1,
                    ActivityTrackers =
                    [
                        new ResourceActivityTrackerModel { Time = 0, ResourceId = 1, ActivityId = c_BookedActivityId, ActivityName = @"Booked", PercentageWorked = 50 },
                    ],
                });
            }

            public CoreViewModel Core { get; }

            public ActivitiesManagerViewModel Manager { get; }

            public GanttActivitySelectorViewModel Gantt { get; }

            public ResourceActivitySelectorViewModel Day { get; }

            public int[] GanttTargets => [.. Gantt.TargetActivities.Select(x => x.Id).Order()];

            public int[] DayTargets => [.. Day.TargetResourceActivities.Select(x => x.Id).Order()];

            public void Select(int activityId)
            {
                Manager.SelectedActivities[activityId] = Core.RawActivities.Single(x => x.Id == activityId);
                Manager.HasSelectedActivities = true;
            }

            public Task SelectorsShouldFollowThePlanAsync(string command) =>
                WaitUntilAsync(
                    () =>
                    {
                        int[] plan = [.. Core.RawActivities.Select(x => x.Id).Order()];
                        return GanttTargets.SequenceEqual(plan) && DayTargets.SequenceEqual(plan);
                    },
                    $@"the selectors did not follow the plan after the command to {command}");

            public void Dispose()
            {
                Day.Dispose();
                Gantt.Dispose();
                Manager.Dispose();
                Core.Dispose();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AddManagedActivityCommand_Given_ASelector_Then_TheNewActivityReachesIt(bool autoCompile)
        {
            using var harness = new Harness(autoCompile);

            harness.Manager.AddManagedActivityCommand.Execute(null);
            await WaitUntilAsync(() => harness.Core.RawActivities.Count == c_ActivityCount + 1, @"the activity was not added");

            await harness.SelectorsShouldFollowThePlanAsync(@"add");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InsertManagedActivityCommand_Given_ASelector_Then_TheNewActivityReachesIt(bool autoCompile)
        {
            using var harness = new Harness(autoCompile);
            harness.Select(3);

            harness.Manager.InsertManagedActivityCommand.Execute(null);
            await WaitUntilAsync(() => harness.Core.RawActivities.Count == c_ActivityCount + 1, @"the activity was not inserted");

            await harness.SelectorsShouldFollowThePlanAsync(@"insert");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DuplicateManagedActivityCommand_Given_ASelector_Then_TheNewActivityReachesIt(bool autoCompile)
        {
            using var harness = new Harness(autoCompile);
            harness.Select(3);

            harness.Manager.DuplicateManagedActivityCommand.Execute(null);
            await WaitUntilAsync(() => harness.Core.RawActivities.Count == c_ActivityCount + 1, @"the activity was not duplicated");

            await harness.SelectorsShouldFollowThePlanAsync(@"duplicate");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RemoveManagedActivitiesCommand_Given_ABooking_Then_TheBookingGoes(bool autoCompile)
        {
            using var harness = new Harness(autoCompile);
            harness.Select(c_BookedActivityId);

            harness.Manager.RemoveManagedActivitiesCommand.Execute(null);
            await WaitUntilAsync(() => harness.Core.RawActivities.Count == c_ActivityCount - 1, @"the activity was not removed");

            await harness.SelectorsShouldFollowThePlanAsync(@"remove");
            harness.Day.SelectedResourceActivityIds.ShouldBeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AddMilestoneCommand_Given_ASelector_Then_TheMilestoneReachesIt(bool autoCompile)
        {
            using var harness = new Harness(autoCompile);
            harness.Select(3);

            harness.Manager.AddMilestoneCommand.Execute(null);
            await WaitUntilAsync(() => harness.Core.RawActivities.Count == c_ActivityCount + 1, @"the milestone was not added");

            await harness.SelectorsShouldFollowThePlanAsync(@"add a milestone");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AddManagedActivityCommand_Given_TwoInARow_Then_BothReachTheSelector(bool autoCompile)
        {
            using var harness = new Harness(autoCompile);

            harness.Manager.AddManagedActivityCommand.Execute(null);
            await harness.SelectorsShouldFollowThePlanAsync(@"add");
            harness.Manager.AddManagedActivityCommand.Execute(null);
            await WaitUntilAsync(() => harness.Core.RawActivities.Count == c_ActivityCount + 2, @"the second activity was not added");

            await harness.SelectorsShouldFollowThePlanAsync(@"add a second time");
        }
    }
}
