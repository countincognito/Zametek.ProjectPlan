using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The parts of the plan that the Gantt chart and the resource chart do not show, by one rule for both charts, so that
    /// they cannot disagree. A resource or a work stream is hidden when its switch IsHiddenFromCharts is on. An activity is
    /// hidden when one of these is true:
    /// <list type="bullet">
    /// <item>its own switch is on;</item>
    /// <item>it has one or more target work streams, and each of them is hidden;</item>
    /// <item>the schedule put it on one or more resources, and each of them is a hidden resource.</item>
    /// </list>
    /// An activity with no target work stream is never hidden by a work stream. An activity on a spare resource (one with no
    /// id, which the series "Resource 1" shows) is never hidden by a resource. The resources of an activity are those that
    /// the schedule put it on, and not its target resources. The filter changes nothing but the two charts: no schedule, no
    /// metric and no other output.
    /// </summary>
    public sealed class HiddenFromCharts
    {
        private HiddenFromCharts(
            HashSet<int> activityIds,
            HashSet<int> resourceIds,
            HashSet<int> workStreamIds)
        {
            ActivityIds = activityIds;
            ResourceIds = resourceIds;
            WorkStreamIds = workStreamIds;
        }

        /// <summary>
        /// Nothing hidden: what a chart uses when it shows the hidden parts.
        /// </summary>
        public static HiddenFromCharts None { get; } = new([], [], []);

        public IReadOnlySet<int> ActivityIds { get; }

        public IReadOnlySet<int> ResourceIds { get; }

        public IReadOnlySet<int> WorkStreamIds { get; }

        public bool IsActivityHidden(int activityId) => ActivityIds.Contains(activityId);

        // A spare resource has the id default, and it is never hidden.
        public bool IsResourceHidden(int resourceId) => resourceId != default && ResourceIds.Contains(resourceId);

        // The group of the activities with no work stream in the Gantt chart has the id default, and it is never hidden.
        public bool IsWorkStreamHidden(int workStreamId) => workStreamId != default && WorkStreamIds.Contains(workStreamId);

        /// <summary>
        /// The hidden parts of the plan of the core: the switches of the live activities, resources and work streams, with
        /// the compile and its schedules. A switch needs no compile, and so a chart reads it from the live plan.
        /// </summary>
        public static HiddenFromCharts Find(ICoreViewModel coreViewModel)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            return Find(
                coreViewModel.RawActivities.Where(x => x.IsHiddenFromCharts).Select(x => x.Id),
                coreViewModel.GraphCompilation.DependentActivities,
                coreViewModel.ResourceSettings,
                coreViewModel.WorkStreamSettings,
                coreViewModel.ResourceSeriesSet.ResourceSchedules);
        }

        /// <summary>
        /// The hidden parts of a plan.
        /// </summary>
        /// <param name="switchedActivityIds">The activities whose own switch is on.</param>
        /// <param name="activities">The activities of the compile: their target work streams.</param>
        /// <param name="resourceSettings">The switches of the resources.</param>
        /// <param name="workStreamSettings">The switches of the work streams.</param>
        /// <param name="resourceSchedules">The schedules of the compile: the resources that each activity is on.</param>
        public static HiddenFromCharts Find(
            IEnumerable<int> switchedActivityIds,
            IEnumerable<IDependentActivity> activities,
            ResourceSettingsModel resourceSettings,
            WorkStreamSettingsModel workStreamSettings,
            IEnumerable<ResourceScheduleModel> resourceSchedules)
        {
            ArgumentNullException.ThrowIfNull(switchedActivityIds);
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(resourceSettings);
            ArgumentNullException.ThrowIfNull(workStreamSettings);
            ArgumentNullException.ThrowIfNull(resourceSchedules);

            HashSet<int> resourceIds = [.. resourceSettings.Resources.Where(x => x.IsHiddenFromCharts).Select(x => x.Id)];
            HashSet<int> workStreamIds = [.. workStreamSettings.WorkStreams.Where(x => x.IsHiddenFromCharts).Select(x => x.Id)];
            HashSet<int> activityIds = [.. switchedActivityIds];

            // The resources that the schedule put each activity on.
            Dictionary<int, List<int>> resourcesOfActivities = [];

            foreach (ResourceScheduleModel resourceSchedule in resourceSchedules)
            {
                foreach (ScheduledActivityModel scheduledActivity in resourceSchedule.ScheduledActivities)
                {
                    if (!resourcesOfActivities.TryGetValue(scheduledActivity.Id, out List<int>? resourcesOfActivity))
                    {
                        resourcesOfActivity = [];
                        resourcesOfActivities.Add(scheduledActivity.Id, resourcesOfActivity);
                    }
                    resourcesOfActivity.Add(resourceSchedule.Resource.Id);
                }
            }

            foreach (IDependentActivity activity in activities)
            {
                if (activityIds.Contains(activity.Id))
                {
                    continue;
                }

                bool isHiddenByWorkStreams = activity.TargetWorkStreams.Count > 0
                    && activity.TargetWorkStreams.All(workStreamIds.Contains);

                bool isHiddenByResources = resourcesOfActivities.TryGetValue(activity.Id, out List<int>? resourcesOfActivity)
                    && resourcesOfActivity.All(x => x != default && resourceIds.Contains(x));

                if (isHiddenByWorkStreams || isHiddenByResources)
                {
                    activityIds.Add(activity.Id);
                }
            }

            return new HiddenFromCharts(activityIds, resourceIds, workStreamIds);
        }

        /// <summary>
        /// A copy of an allocation of a series of the resource chart, with no day of a hidden activity. The days of an
        /// activity are from its start (0 for a start before 0) to its finish - 1. For a resource of the type None or Direct,
        /// and for a spare resource, they go in each allocation mode: this is how the library removes the days of an
        /// activity with no cost from the cost (ResourceScheduleBuilder of Zametek.Maths.Graphs), and the days between two
        /// activities stay. For an Indirect resource, they go only in the mode Activity, which shows only the days with an
        /// activity: the resource works on each day of its range.
        /// </summary>
        public List<bool> VisibleAllocation(
            ResourceSeriesModel series,
            AllocationMode allocationMode,
            IReadOnlyList<bool> allocation)
        {
            ArgumentNullException.ThrowIfNull(series);
            ArgumentNullException.ThrowIfNull(allocation);

            List<bool> visible = [.. allocation];

            if (ActivityIds.Count == 0
                || (series.InterActivityAllocationType == InterActivityAllocationType.Indirect
                    && allocationMode != AllocationMode.Activity))
            {
                return visible;
            }

            foreach (ScheduledActivityModel scheduledActivity in series.ResourceSchedule.ScheduledActivities)
            {
                if (!ActivityIds.Contains(scheduledActivity.Id))
                {
                    continue;
                }

                int start = Math.Max(scheduledActivity.StartTime, 0);
                int finish = Math.Min(scheduledActivity.FinishTime, visible.Count);

                for (int day = start; day < finish; day++)
                {
                    visible[day] = false;
                }
            }

            return visible;
        }
    }
}
