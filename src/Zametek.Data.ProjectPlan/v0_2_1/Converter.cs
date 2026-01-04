using AutoMapper;

namespace Zametek.Data.ProjectPlan.v0_2_1
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            IMapper mapper,
            v0_2_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);
            GraphCompilationErrorsModel? errors = null;

            if (project.GraphCompilation?.Errors != null)
            {
                errors = new GraphCompilationErrorsModel
                {
                    AllResourcesExplicitTargetsButNotAllActivitiesTargeted = project.GraphCompilation.Errors.AllResourcesExplicitTargetsButNotAllActivitiesTargeted,
                    CircularDependencies = project.GraphCompilation.Errors.CircularDependencies,
                    MissingDependencies = project.GraphCompilation.Errors.MissingDependencies,
                    InvalidConstraints = [],
                };
            }

            return new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                DependentActivities = mapper.Map<List<v0_1_0.DependentActivityModel>, List<DependentActivityModel>>(project.DependentActivities),
                ArrowGraphSettings = project.ArrowGraphSettings,
                ResourceSettings = project.ResourceSettings,
                GraphCompilation = new GraphCompilationModel
                {
                    DependentActivities = mapper.Map<List<v0_1_0.DependentActivityModel>, List<DependentActivityModel>>(project.GraphCompilation?.DependentActivities ?? []),
                    ResourceSchedules = mapper.Map<List<v0_1_0.ResourceScheduleModel>, List<ResourceScheduleModel>>(project.GraphCompilation?.ResourceSchedules ?? []),
                    Errors = errors,
                    CyclomaticComplexity = project.GraphCompilation?.CyclomaticComplexity ?? default,
                    Duration = project.GraphCompilation?.Duration ?? default,
                },
                ArrowGraph = mapper.Map<v0_1_0.ArrowGraphModel, ArrowGraphModel>(project.ArrowGraph ?? new v0_1_0.ArrowGraphModel()),
                HasStaleOutputs = project.HasStaleOutputs,
            };
        }
    }
}
