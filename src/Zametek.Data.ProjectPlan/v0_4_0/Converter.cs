using AutoMapper;
using Zametek.Resource.ProjectPlan;

namespace Zametek.Data.ProjectPlan.v0_4_0
{
    public static class Converter
    {
        private static readonly EqualityComparer<ActivityTrackerModel> s_ActivityTrackerEqualityComparer =
            EqualityComparer<ActivityTrackerModel>.Create(
                    (x, y) =>
                    {
                        if (x is null)
                        {
                            return false;
                        }
                        if (y is null)
                        {
                            return false;
                        }
                        return x.ActivityId == y.ActivityId && x.PercentageComplete == y.PercentageComplete;
                    },
                    x => x.ActivityId.GetHashCode() + x.PercentageComplete.GetHashCode());

        private static readonly EqualityComparer<ResourceActivityTrackerModel> s_ResourceActivityTrackerEqualityComparer =
            EqualityComparer<ResourceActivityTrackerModel>.Create(
                    (x, y) =>
                    {
                        if (x is null)
                        {
                            return false;
                        }
                        if (y is null)
                        {
                            return false;
                        }
                        return x.Time == y.Time && x.ResourceId == y.ResourceId && x.ActivityId == y.ActivityId;
                    },
                    x => x.Time.GetHashCode() + x.ResourceId.GetHashCode() + x.ActivityId.GetHashCode());

        public static ProjectPlanModel Upgrade(
            IMapper mapper,
            v0_3_2.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(projectPlan);

            var plan = new ProjectPlanModel
            {
                ProjectStart = projectPlan.ProjectStart,
                DependentActivities = mapper.Map<List<v0_3_2.DependentActivityModel>, List<DependentActivityModel>>(projectPlan.DependentActivities),
                ArrowGraphSettings = projectPlan.ArrowGraphSettings ?? new v0_1_0.ArrowGraphSettingsModel(),
                ResourceSettings = mapper.Map<v0_3_2.ResourceSettingsModel, ResourceSettingsModel>(projectPlan.ResourceSettings ?? new v0_3_2.ResourceSettingsModel()),
                WorkStreamSettings = projectPlan.WorkStreamSettings ?? new v0_3_2.WorkStreamSettingsModel(),
                DisplaySettings = new DisplaySettingsModel(),
                GraphCompilation = mapper.Map<v0_3_2.GraphCompilationModel, GraphCompilationModel>(projectPlan.GraphCompilation ?? new v0_3_2.GraphCompilationModel()),
                ArrowGraph = mapper.Map<v0_3_2.ArrowGraphModel, ArrowGraphModel>(projectPlan.ArrowGraph ?? new v0_3_2.ArrowGraphModel()),
                HasStaleOutputs = projectPlan.HasStaleOutputs,
            };

            // Check all old tracker models and ensure the time values are consistent.
            List<int> trackerSizes = plan
                .DependentActivities
                .Select(x => x.Activity.Trackers.Count)
                .Distinct()
                .ToList();

            if (trackerSizes.Count > 1)
            {
                throw new InvalidOperationException(Messages.Message_NumberOfTrackersAcrossAllActivitiesNotIdentical);
            }

            if (trackerSizes.Count == 1)
            {
                int trackerSize = trackerSizes[0];

                foreach (DependentActivityModel dependentActivity in plan.DependentActivities)
                {
                    ActivityModel activity = dependentActivity.Activity;

                    for (int i = 0; i < trackerSize; i++)
                    {
                        activity.Trackers[i] = activity.Trackers[i] with
                        {
                            Time = i,
                        };
                    }
                }
            }

            // Convert the old tracker models into the new tracker models.
            Dictionary<int, ResourceModel> resourceLookup = plan.ResourceSettings.Resources.ToDictionary(x => x.Id);

            // Capture all the activity trackers across all resources.
            List<ResourceActivityTrackerModel> resourceActivityTrackers = [];

            // Cycle through the old activity trackers to allocate resource usage.
            foreach (v0_3_2.DependentActivityModel oldActivityModel in projectPlan.DependentActivities)
            {
                List<v0_3_0.TrackerModel> oldActivityTrackers = oldActivityModel.Activity.Trackers;

                // As part of the conversion, check when a resource has worked on more than one activity
                // on a particular day.
                foreach (int resourceId in oldActivityModel.Activity.AllocatedToResources)
                {
                    if (resourceLookup.TryGetValue(resourceId, out var resource))
                    {
                        foreach (v0_3_0.TrackerModel oldActivityTracker in oldActivityTrackers.Where(x => x.IsIncluded))
                        {
                            resourceActivityTrackers.Add(
                                new ResourceActivityTrackerModel
                                {
                                    ResourceId = resourceId,
                                    Time = oldActivityTracker.Time,
                                    ActivityId = oldActivityTracker.ActivityId,
                                    PercentageWorked = 100,
                                });

                            // Since we have no other way of telling, just assume 100% of time worked on an activity.
                            // Even if there are several in one day.
                        }
                    }
                }
            }

            // Clear the resource activity trackers that have the same keys.
            resourceActivityTrackers = resourceActivityTrackers
                .OrderBy(x => x.Time)
                .ThenBy(x => x.ResourceId)
                .ThenBy(x => x.ActivityId)
                .Distinct(s_ResourceActivityTrackerEqualityComparer)
                .ToList();

            // Cycle through each resource and collate the new activity trackers
            // according to time and resource ID.
            foreach (ResourceModel resource in resourceLookup.Values)
            {
                int resourceId = resource.Id;

                resource.Trackers.Clear();

                List<int> times = resourceActivityTrackers
                    .Where(x => x.ResourceId == resourceId)
                    .Select(x => x.Time)
                    .Distinct()
                    .ToList();

                // For each time period in a resource's tracker list,
                // gather the trackers for each activity that the resource
                // has worked on.
                foreach (int time in times)
                {
                    var resourceTracker = new ResourceTrackerModel
                    {
                        ResourceId = resourceId,
                        Time = time,
                    };

                    List<ResourceActivityTrackerModel> trackers = resourceActivityTrackers
                        .Where(x => x.Time == time && x.ResourceId == resourceId)
                        .ToList();

                    resourceTracker.ActivityTrackers.AddRange(trackers);

                    resource.Trackers.Add(resourceTracker);
                }
            }

            // Cycle through the new activity trackers and clear out unnecessary values.
            foreach (DependentActivityModel dependentActivityModel in plan.DependentActivities)
            {
                List<ActivityTrackerModel> activityTrackers = dependentActivityModel.Activity.Trackers;

                // Remove zero values.
                activityTrackers.RemoveAll(x => x.PercentageComplete == 0);

                // Select the first instances of duplicated values (ignoring time values).
                IEnumerable<ActivityTrackerModel> replacement = activityTrackers
                    .OrderBy(x => x.Time)
                    .Distinct(s_ActivityTrackerEqualityComparer)
                    .ToList();

                // Now replace the old values.
                activityTrackers.Clear();
                activityTrackers.AddRange(replacement);
            }

            Dictionary<int, ActivityModel> activtyLookup =
                plan.DependentActivities
                .Select(x => x.Activity)
                .ToDictionary(x => x.Id);

            // Cycle through the new activity trackers in the graph compilation and
            // replace the old values.
            foreach (DependentActivityModel dependentActivityModel in plan.GraphCompilation.DependentActivities)
            {
                dependentActivityModel.Activity.Trackers.Clear();

                if (activtyLookup.TryGetValue(dependentActivityModel.Activity.Id, out ActivityModel? activity))
                {
                    dependentActivityModel.Activity.Trackers.AddRange(activity.Trackers);
                }
            }

            // Cycle through the new activity trackers in the arrow graph and
            // replace the old values.
            foreach (ActivityEdgeModel edgeModel in plan.ArrowGraph.Edges)
            {
                edgeModel.Content.Trackers.Clear();
                edgeModel.Content.TargetWorkStreams.Clear();

                if (activtyLookup.TryGetValue(edgeModel.Content.Id, out ActivityModel? activity))
                {
                    edgeModel.Content.Trackers.AddRange(activity.Trackers);
                    edgeModel.Content.TargetWorkStreams.AddRange(activity.TargetWorkStreams);
                }
            }

            return plan;
        }
    }
}
