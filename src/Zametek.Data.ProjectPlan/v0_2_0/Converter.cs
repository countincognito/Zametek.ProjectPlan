using System;
using System.Linq;

namespace Zametek.Data.ProjectPlan.v0_2_0
{
    public static class Converter
    {
        public static ProjectPlanModel Upgrade(v0_1_0.ProjectPlanModel projectPlan)
        {
            if (projectPlan == null)
            {
                throw new ArgumentNullException(nameof(projectPlan));
            }

            GraphCompilationErrorsModel errors = null;
            bool errorsExist = projectPlan.GraphCompilation.AllResourcesExplicitTargetsButNotAllActivitiesTargeted
                || projectPlan.GraphCompilation.CircularDependencies.Any()
                || projectPlan.GraphCompilation.MissingDependencies.Any();

            if (errorsExist)
            {
                errors = new GraphCompilationErrorsModel
                {
                    AllResourcesExplicitTargetsButNotAllActivitiesTargeted = projectPlan.GraphCompilation.AllResourcesExplicitTargetsButNotAllActivitiesTargeted,
                    CircularDependencies = projectPlan.GraphCompilation.CircularDependencies,
                    MissingDependencies = projectPlan.GraphCompilation.MissingDependencies,
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
                    DependentActivities = projectPlan.GraphCompilation.DependentActivities,
                    ResourceSchedules = projectPlan.GraphCompilation.ResourceSchedules,
                    Errors = errors,
                    CyclomaticComplexity = projectPlan.GraphCompilation.CyclomaticComplexity,
                    Duration = projectPlan.GraphCompilation.Duration,
                },
                ArrowGraph = projectPlan.ArrowGraph,
                HasStaleOutputs = projectPlan.HasStaleOutputs,
            };
        }
    }
}
