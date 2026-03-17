namespace Zametek.Data.ProjectPlan.v0_2_1
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            VersionMapper mapper,
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
                DependentActivities = [.. project.DependentActivities.Select(mapper.FromV0_1_0ToV0_2_1)],
                ArrowGraphSettings = project.ArrowGraphSettings,
                ResourceSettings = project.ResourceSettings,
                GraphCompilation = new GraphCompilationModel
                {
                    DependentActivities = [.. (project.GraphCompilation?.DependentActivities ?? []).Select(mapper.FromV0_1_0ToV0_2_1)],
                    ResourceSchedules = [.. (project.GraphCompilation?.ResourceSchedules ?? []).Select(mapper.FromV0_1_0ToV0_2_1)],
                    Errors = errors,
                    CyclomaticComplexity = project.GraphCompilation?.CyclomaticComplexity ?? default,
                    Duration = project.GraphCompilation?.Duration ?? default,
                },
                ArrowGraph = mapper.FromV0_1_0ToV0_2_1(project.ArrowGraph ?? new v0_1_0.ArrowGraphModel()),
                HasStaleOutputs = project.HasStaleOutputs,
            };
        }
    }
}
