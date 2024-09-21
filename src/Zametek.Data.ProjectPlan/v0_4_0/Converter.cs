using AutoMapper;

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
                GraphCompilation = mapper.Map<v0_3_2.GraphCompilationModel, GraphCompilationModel>(projectPlan.GraphCompilation ?? new v0_3_2.GraphCompilationModel()),
                ArrowGraph = mapper.Map<v0_3_2.ArrowGraphModel, ArrowGraphModel>(projectPlan.ArrowGraph ?? new v0_3_2.ArrowGraphModel()),
                HasStaleOutputs = projectPlan.HasStaleOutputs,
            };


            Dictionary<int, ResourceModel> resourceLookup = plan.ResourceSettings.Resources.ToDictionary(x => x.Id);



            // Cycle through the activity trackers and clear out unnecessary values.
            foreach (DependentActivityModel activityModel in plan.DependentActivities)
            {
                List<ActivityTrackerModel> activityTrackers = activityModel.Activity.Trackers;

                // First remove zero values.
                activityTrackers.RemoveAll(x => x.PercentageComplete == 0);

                // Now select the first instances of duplicated values (ignoring time values).
                IEnumerable<ActivityTrackerModel> replacement = activityTrackers.Distinct(s_ActivityTrackerEqualityComparer).ToList();

                // Now replace the old values.
                activityTrackers.Clear();
                activityTrackers.AddRange(replacement);



                // As part of the conversion, assume only one resource activity tracking entry.
                foreach (int resourceId in activityModel.Activity.AllocatedToResources)
                {
                    if (resourceLookup.TryGetValue(resourceId, out var resource))
                    {

                        foreach (ActivityTrackerModel activityTracker in activityTrackers)
                        {
                            var resourceTracker = new ResourceTrackerModel
                            {
                                ResourceId = resourceId,
                                Time = activityTracker.Time
                            };
                            resourceTracker.ActivityTrackers.Add(
                                new ResourceActivityTrackerModel
                                {
                                    ResourceId = resourceId,
                                    Time = activityTracker.Time,
                                    ActivityId = activityTracker.ActivityId,
                                    PercentageWorked = 100,
                                });


                            resource.Trackers.Add(resourceTracker);
                        }




                    }
                }











            }

            return plan;
        }
    }
}
