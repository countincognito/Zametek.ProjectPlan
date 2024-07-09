using AutoMapper;

namespace Zametek.Data.ProjectPlan.v0_3_1
{
    public static class Converter
    {
        public static ProjectPlanModel Upgrade(
            IMapper mapper,
            v0_3_0.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(projectPlan);

            return new ProjectPlanModel
            {
                ProjectStart = projectPlan.ProjectStart,
                DependentActivities = projectPlan.DependentActivities ?? [],
                ArrowGraphSettings = projectPlan.ArrowGraphSettings ?? new v0_1_0.ArrowGraphSettingsModel(),
                ResourceSettings = mapper.Map<v0_1_0.ResourceSettingsModel, ResourceSettingsModel>(projectPlan.ResourceSettings ?? new v0_1_0.ResourceSettingsModel()),
                GraphCompilation = mapper.Map<v0_3_0.GraphCompilationModel, GraphCompilationModel>(projectPlan.GraphCompilation ?? new v0_3_0.GraphCompilationModel()),
                ArrowGraph = projectPlan.ArrowGraph ?? new v0_3_0.ArrowGraphModel(),
                HasStaleOutputs = projectPlan.HasStaleOutputs,
            };
        }
    }
}
