using AutoMapper;

namespace Zametek.Data.ProjectPlan.v0_4_3
{
    public static class Converter
    {
        public static ProjectPlanModel Upgrade(
            IMapper mapper,
            v0_4_2.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(projectPlan);

            List<DependentActivityModel> activities = mapper.Map<List<v0_4_2.DependentActivityModel>, List<DependentActivityModel>>(projectPlan.DependentActivities);

            for (int i = 0; i < activities.Count; i++)
            {
                activities[i].PlanningDependencies.Clear();
                activities[i].PlanningDependencies.AddRange(projectPlan.DependentActivities[i].ManualDependencies);
            }

            var plan = new ProjectPlanModel
            {
                ProjectStart = projectPlan.ProjectStart,
                Today = projectPlan.ProjectStart,
                DependentActivities = activities,
                ArrowGraphSettings = projectPlan.ArrowGraphSettings ?? new(),
                ResourceSettings = projectPlan.ResourceSettings ?? new(),
                WorkStreamSettings = projectPlan.WorkStreamSettings ?? new(),
                DisplaySettings = projectPlan.DisplaySettings ?? new(),
                GraphCompilation = mapper.Map<v0_4_2.GraphCompilationModel, GraphCompilationModel>(projectPlan.GraphCompilation ?? new v0_4_2.GraphCompilationModel()),
                ArrowGraph = projectPlan.ArrowGraph ?? new(),
                HasStaleOutputs = projectPlan.HasStaleOutputs,
            };

            return plan;
        }
    }
}
