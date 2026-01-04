namespace Zametek.Data.ProjectPlan.v0_2_0
{
    public static class Converter
    {
        public static ProjectModel Upgrade(v0_1_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            GraphCompilationErrorsModel? errors = null;
            bool errorsExist = (project.GraphCompilation?.AllResourcesExplicitTargetsButNotAllActivitiesTargeted ?? false)
                || (project.GraphCompilation?.CircularDependencies.Any() ?? false)
                || (project.GraphCompilation?.MissingDependencies.Any() ?? false);

            if (errorsExist)
            {
                errors = new GraphCompilationErrorsModel
                {
                    AllResourcesExplicitTargetsButNotAllActivitiesTargeted = project.GraphCompilation?.AllResourcesExplicitTargetsButNotAllActivitiesTargeted ?? false,
                    CircularDependencies = project.GraphCompilation?.CircularDependencies ?? [],
                    MissingDependencies = project.GraphCompilation?.MissingDependencies ?? [],
                };
            }

            return new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                DependentActivities = project.DependentActivities,
                ArrowGraphSettings = project.ArrowGraphSettings,
                ResourceSettings = project.ResourceSettings,
                GraphCompilation = new GraphCompilationModel
                {
                    DependentActivities = project.GraphCompilation?.DependentActivities ?? [],
                    ResourceSchedules = project.GraphCompilation?.ResourceSchedules ?? [],
                    Errors = errors,
                    CyclomaticComplexity = project.GraphCompilation?.CyclomaticComplexity ?? default,
                    Duration = project.GraphCompilation?.Duration ?? default,
                },
                ArrowGraph = project.ArrowGraph,
                HasStaleOutputs = project.HasStaleOutputs,
            };
        }
    }
}
