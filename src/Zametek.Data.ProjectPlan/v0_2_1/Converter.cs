using AutoMapper;

namespace Zametek.Data.ProjectPlan.v0_2_1
{
    public static class Converter
    {
        public static ProjectPlanModel Upgrade(
            IMapper mapper,
            v0_2_0.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(projectPlan);
            GraphCompilationErrorsModel? errors = null;

            if (projectPlan.GraphCompilation?.Errors != null)
            {
                errors = new GraphCompilationErrorsModel
                {
                    AllResourcesExplicitTargetsButNotAllActivitiesTargeted = projectPlan.GraphCompilation.Errors.AllResourcesExplicitTargetsButNotAllActivitiesTargeted,
                    CircularDependencies = projectPlan.GraphCompilation.Errors.CircularDependencies,
                    MissingDependencies = projectPlan.GraphCompilation.Errors.MissingDependencies,
                    InvalidConstraints = [],
                };
            }

            return new ProjectPlanModel
            {
                ProjectStart = projectPlan.ProjectStart,
                DependentActivities = mapper.Map<List<v0_1_0.DependentActivityModel>, List<DependentActivityModel>>(projectPlan.DependentActivities),
                ArrowGraphSettings = projectPlan.ArrowGraphSettings,
                ResourceSettings = projectPlan.ResourceSettings,
                GraphCompilation = new GraphCompilationModel
                {
                    DependentActivities = mapper.Map<List<v0_1_0.DependentActivityModel>, List<DependentActivityModel>>(projectPlan.GraphCompilation?.DependentActivities ?? []),
                    ResourceSchedules = mapper.Map<List<v0_1_0.ResourceScheduleModel>, List<ResourceScheduleModel>>(projectPlan.GraphCompilation?.ResourceSchedules ?? []),
                    Errors = errors,
                    CyclomaticComplexity = projectPlan.GraphCompilation?.CyclomaticComplexity ?? default,
                    Duration = projectPlan.GraphCompilation?.Duration ?? default,
                },
                ArrowGraph = mapper.Map<v0_1_0.ArrowGraphModel, ArrowGraphModel>(projectPlan.ArrowGraph ?? new v0_1_0.ArrowGraphModel()),
                HasStaleOutputs = projectPlan.HasStaleOutputs,
            };
        }
    }
}
