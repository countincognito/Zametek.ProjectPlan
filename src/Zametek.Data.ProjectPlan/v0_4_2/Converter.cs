using AutoMapper;

namespace Zametek.Data.ProjectPlan.v0_4_2
{
    public static class Converter
    {
        public static ProjectPlanModel Upgrade(
            IMapper mapper,
            v0_4_1.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(projectPlan);

            var plan = new ProjectPlanModel
            {
                ProjectStart = projectPlan.ProjectStart,
                Today = projectPlan.ProjectStart,
                DependentActivities = mapper.Map<List<v0_4_0.DependentActivityModel>, List<DependentActivityModel>>(projectPlan.DependentActivities),
                ArrowGraphSettings = projectPlan.ArrowGraphSettings ?? new(),
                ResourceSettings = projectPlan.ResourceSettings ?? new(),
                WorkStreamSettings = projectPlan.WorkStreamSettings ?? new(),
                DisplaySettings = projectPlan.DisplaySettings ?? new(),
                GraphCompilation = mapper.Map<v0_4_0.GraphCompilationModel, GraphCompilationModel>(projectPlan.GraphCompilation ?? new v0_4_0.GraphCompilationModel()),
                ArrowGraph = projectPlan.ArrowGraph ?? new(),
                HasStaleOutputs = projectPlan.HasStaleOutputs,
            };

            return plan;
        }
    }
}
