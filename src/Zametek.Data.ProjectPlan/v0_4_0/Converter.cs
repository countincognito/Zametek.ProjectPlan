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

            // Cycle through the activity trackers and clear out unnecessary values.
            foreach (DependentActivityModel activityModel in plan.DependentActivities)
            {
                List<ActivityTrackerModel> trackers = activityModel.Activity.Trackers;

                // First remove zero values.
                trackers.RemoveAll(x => x.PercentageComplete == 0);

                // Now select the first instances of duplicated values (ignoring time values).
                IEnumerable<ActivityTrackerModel> replacement = trackers.Distinct(s_ActivityTrackerEqualityComparer).ToList();

                // Now replace the old values.
                trackers.Clear();
                trackers.AddRange(replacement);
            }

            return plan;
        }
    }
}
