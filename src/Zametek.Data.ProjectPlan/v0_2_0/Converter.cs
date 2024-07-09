namespace Zametek.Data.ProjectPlan.v0_2_0
{
    public static class Converter
    {
        public static ProjectPlanModel Upgrade(v0_1_0.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(projectPlan);
            GraphCompilationErrorsModel? errors = null;
            bool errorsExist = (projectPlan.GraphCompilation?.AllResourcesExplicitTargetsButNotAllActivitiesTargeted ?? false)
                || (projectPlan.GraphCompilation?.CircularDependencies.Any() ?? false)
                || (projectPlan.GraphCompilation?.MissingDependencies.Any() ?? false);

            if (errorsExist)
            {
                errors = new GraphCompilationErrorsModel
                {
                    AllResourcesExplicitTargetsButNotAllActivitiesTargeted = projectPlan.GraphCompilation?.AllResourcesExplicitTargetsButNotAllActivitiesTargeted ?? false,
                    CircularDependencies = projectPlan.GraphCompilation?.CircularDependencies ?? [],
                    MissingDependencies = projectPlan.GraphCompilation?.MissingDependencies ?? [],
                };
            }

            return new ProjectPlanModel
            {
                ProjectStart = projectPlan.ProjectStart,
                DependentActivities = projectPlan.DependentActivities,
                ArrowGraphSettings = projectPlan.ArrowGraphSettings,
                ResourceSettings = projectPlan.ResourceSettings,
                GraphCompilation = new GraphCompilationModel
                {
                    DependentActivities = projectPlan.GraphCompilation?.DependentActivities ?? [],
                    ResourceSchedules = projectPlan.GraphCompilation?.ResourceSchedules ?? [],
                    Errors = errors,
                    CyclomaticComplexity = projectPlan.GraphCompilation?.CyclomaticComplexity ?? default,
                    Duration = projectPlan.GraphCompilation?.Duration ?? default,
                },
                ArrowGraph = projectPlan.ArrowGraph,
                HasStaleOutputs = projectPlan.HasStaleOutputs,
            };
        }
    }
}
