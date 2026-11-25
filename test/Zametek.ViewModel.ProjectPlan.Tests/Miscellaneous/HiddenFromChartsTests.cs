using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The rule of the parts of the plan that the Gantt chart and the resource chart do not show, and the days that the
    /// resource chart removes for a hidden activity.
    /// </summary>
    public class HiddenFromChartsTests
    {
        private const int c_HiddenResource = 1;
        private const int c_Resource = 2;
        private const int c_HiddenWorkStream = 1;
        private const int c_WorkStream = 2;

        private static readonly ResourceSettingsModel s_Resources = new()
        {
            Resources =
            [
                new ResourceModel { Id = c_HiddenResource, IsHiddenFromCharts = true },
                new ResourceModel { Id = c_Resource },
            ],
        };

        private static readonly WorkStreamSettingsModel s_WorkStreams = new()
        {
            WorkStreams =
            [
                new WorkStreamModel { Id = c_HiddenWorkStream, IsHiddenFromCharts = true },
                new WorkStreamModel { Id = c_WorkStream },
            ],
        };

        [Fact]
        public void Find_Given_TheSwitchOfAnActivity_Then_ItIsHidden()
        {
            HiddenFromCharts hidden = Find([7], [Activity(7), Activity(8)], []);

            hidden.ActivityIds.ShouldBe([7]);
        }

        [Fact]
        public void Find_Given_AnActivityWhoseWorkStreamsAreAllHidden_Then_ItIsHidden()
        {
            HiddenFromCharts hidden = Find([], [Activity(7, c_HiddenWorkStream)], []);

            hidden.IsActivityHidden(7).ShouldBeTrue();
        }

        [Fact]
        public void Find_Given_AnActivityWithAWorkStreamThatIsNotHidden_Then_ItIsShown()
        {
            // The example of the user: an activity in AA and BB, with BB hidden, stays.
            HiddenFromCharts hidden = Find([], [Activity(7, c_HiddenWorkStream, c_WorkStream)], []);

            hidden.IsActivityHidden(7).ShouldBeFalse();
        }

        [Fact]
        public void Find_Given_AnActivityWithNoWorkStream_Then_NoWorkStreamHidesIt()
        {
            HiddenFromCharts hidden = Find([], [Activity(7)], []);

            hidden.IsActivityHidden(7).ShouldBeFalse();
        }

        [Fact]
        public void Find_Given_AnActivityThatTheScheduleOnlyPutOnHiddenResources_Then_ItIsHidden()
        {
            HiddenFromCharts hidden = Find([], [Activity(7)], [Schedule(c_HiddenResource, 7)]);

            hidden.IsActivityHidden(7).ShouldBeTrue();
        }

        [Fact]
        public void Find_Given_AnActivityThatTheScheduleAlsoPutOnAResourceThatIsNotHidden_Then_ItIsShown()
        {
            HiddenFromCharts hidden = Find([], [Activity(7)], [Schedule(c_HiddenResource, 7), Schedule(c_Resource, 7)]);

            hidden.IsActivityHidden(7).ShouldBeFalse();
        }

        [Fact]
        public void Find_Given_AnActivityOnASpareResource_Then_NoResourceHidesIt()
        {
            // The spare resources of the schedule (the series "Resource 1") have no id.
            HiddenFromCharts hidden = Find([], [Activity(7)], [Schedule(default, 7)]);

            hidden.IsActivityHidden(7).ShouldBeFalse();
            hidden.IsResourceHidden(default).ShouldBeFalse();
        }

        [Fact]
        public void Find_Given_AnActivityThatTargetsAHiddenResourceAndIsOnAnother_Then_TheScheduleDecides()
        {
            // The resources of an activity are those that the schedule put it on, and not its target resources.
            DependentActivity activity = Activity(7);
            activity.TargetResources.Add(c_HiddenResource);

            HiddenFromCharts hidden = Find([], [activity], [Schedule(c_Resource, 7)]);

            hidden.IsActivityHidden(7).ShouldBeFalse();
        }

        [Fact]
        public void Find_Given_AnActivityOnNoResource_Then_NoResourceHidesIt()
        {
            HiddenFromCharts hidden = Find([], [Activity(7)], [Schedule(c_HiddenResource, 8)]);

            hidden.IsActivityHidden(7).ShouldBeFalse();
            hidden.IsActivityHidden(8).ShouldBeFalse("8 is not an activity of the plan");
        }

        [Fact]
        public void Find_Given_TheSwitchesOfTheSettings_Then_TheHiddenResourcesAndWorkStreamsAreThose()
        {
            HiddenFromCharts hidden = Find([], [], []);

            hidden.ResourceIds.ShouldBe([c_HiddenResource]);
            hidden.WorkStreamIds.ShouldBe([c_HiddenWorkStream]);
            hidden.IsResourceHidden(c_HiddenResource).ShouldBeTrue();
            hidden.IsResourceHidden(c_Resource).ShouldBeFalse();
            hidden.IsWorkStreamHidden(c_HiddenWorkStream).ShouldBeTrue();
            hidden.IsWorkStreamHidden(default).ShouldBeFalse("the group of the activities with no work stream is never hidden");
        }

        [Fact]
        public void None_Then_HidesNothing()
        {
            HiddenFromCharts.None.ActivityIds.ShouldBeEmpty();
            HiddenFromCharts.None.ResourceIds.ShouldBeEmpty();
            HiddenFromCharts.None.WorkStreamIds.ShouldBeEmpty();
        }

        [Theory]
        [InlineData(InterActivityAllocationType.None, AllocationMode.Resource)]
        [InlineData(InterActivityAllocationType.None, AllocationMode.Cost)]
        [InlineData(InterActivityAllocationType.None, AllocationMode.Billing)]
        [InlineData(InterActivityAllocationType.None, AllocationMode.Effort)]
        [InlineData(InterActivityAllocationType.None, AllocationMode.Activity)]
        [InlineData(InterActivityAllocationType.Direct, AllocationMode.Resource)]
        [InlineData(InterActivityAllocationType.Direct, AllocationMode.Cost)]
        [InlineData(InterActivityAllocationType.Direct, AllocationMode.Billing)]
        [InlineData(InterActivityAllocationType.Direct, AllocationMode.Effort)]
        [InlineData(InterActivityAllocationType.Direct, AllocationMode.Activity)]
        [InlineData(InterActivityAllocationType.Indirect, AllocationMode.Activity)]
        public void VisibleAllocation_Given_AHiddenActivity_Then_ItsDaysGoAndTheOtherDaysStay(InterActivityAllocationType type, AllocationMode mode)
        {
            // Activities 1 (days 0 and 1), 2 (days 4 to 6, hidden) and 3 (days 8 and 9). The days between them stay, as they
            // do for an activity with no cost in the library.
            HiddenFromCharts hidden = Find([2], [Activity(1), Activity(2), Activity(3)], []);
            List<bool> allocation = [.. Enumerable.Repeat(true, 10)];

            List<bool> visible = hidden.VisibleAllocation(Series(type, (1, 0, 2), (2, 4, 7), (3, 8, 10)), mode, allocation);

            visible.ShouldBe([true, true, true, true, false, false, false, true, true, true]);
            allocation.ShouldAllBe(x => x, "the list of the series is the list of the Gantt chart, the metrics and the exports too");
        }

        [Theory]
        [InlineData(AllocationMode.Resource)]
        [InlineData(AllocationMode.Cost)]
        [InlineData(AllocationMode.Billing)]
        [InlineData(AllocationMode.Effort)]
        public void VisibleAllocation_Given_AHiddenActivityOfAnIndirectResource_Then_TheResourceKeepsItsDays(AllocationMode mode)
        {
            // An Indirect resource works on each day of its range, with an activity or with none.
            HiddenFromCharts hidden = Find([2], [Activity(2)], []);
            List<bool> allocation = [.. Enumerable.Repeat(true, 10)];

            List<bool> visible = hidden.VisibleAllocation(Series(InterActivityAllocationType.Indirect, (2, 4, 7)), mode, allocation);

            visible.ShouldAllBe(x => x);
        }

        [Fact]
        public void VisibleAllocation_Given_AStartBeforeTheStartOfTheProjectAndAFinishAfterTheList_Then_TheDaysInTheListGo()
        {
            HiddenFromCharts hidden = Find([2], [Activity(2)], []);

            List<bool> visible = hidden.VisibleAllocation(
                Series(InterActivityAllocationType.None, (2, -3, 20)),
                AllocationMode.Resource,
                [.. Enumerable.Repeat(true, 5)]);

            visible.ShouldBe([false, false, false, false, false]);
        }

        [Fact]
        public void VisibleAllocation_Given_AHiddenMilestone_Then_NoDayGoes()
        {
            HiddenFromCharts hidden = Find([2], [Activity(2)], []);

            List<bool> visible = hidden.VisibleAllocation(
                Series(InterActivityAllocationType.Direct, (2, 0, 0), (2, 3, 3)),
                AllocationMode.Resource,
                [.. Enumerable.Repeat(true, 5)]);

            visible.ShouldAllBe(x => x);
        }

        [Fact]
        public void VisibleAllocation_Given_NothingHidden_Then_ACopyOfTheAllocation()
        {
            List<bool> allocation = [true, false, true];

            List<bool> visible = HiddenFromCharts.None.VisibleAllocation(Series(InterActivityAllocationType.None, (1, 0, 3)), AllocationMode.Resource, allocation);

            visible.ShouldBe(allocation);
            visible.ShouldNotBeSameAs(allocation);
        }

        private static HiddenFromCharts Find(
            IEnumerable<int> switchedActivityIds,
            IEnumerable<IDependentActivity> activities,
            IEnumerable<ResourceScheduleModel> resourceSchedules) =>
            HiddenFromCharts.Find(switchedActivityIds, activities, s_Resources, s_WorkStreams, resourceSchedules);

        private static DependentActivity Activity(int id, params int[] targetWorkStreams)
        {
            var activity = new DependentActivity(id, 1, canBeRemoved: false);
            activity.TargetWorkStreams.UnionWith(targetWorkStreams);
            return activity;
        }

        private static ResourceScheduleModel Schedule(int resourceId, params int[] activityIds) => new()
        {
            Resource = new ResourceModel { Id = resourceId },
            ScheduledActivities = [.. activityIds.Select(x => new ScheduledActivityModel { Id = x, StartTime = 0, FinishTime = 1 })],
        };

        private static ResourceSeriesModel Series(InterActivityAllocationType type, params (int Id, int Start, int Finish)[] activities) => new()
        {
            InterActivityAllocationType = type,
            ResourceSchedule = new ResourceScheduleModel
            {
                Resource = new ResourceModel { Id = c_Resource },
                ScheduledActivities = [.. activities.Select(x => new ScheduledActivityModel { Id = x.Id, StartTime = x.Start, FinishTime = x.Finish })],
            },
        };
    }
}
