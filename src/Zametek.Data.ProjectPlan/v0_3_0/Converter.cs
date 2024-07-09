using AutoMapper;
using System.Globalization;
using System.Text;

namespace Zametek.Data.ProjectPlan.v0_3_0
{
    public static class Converter
    {
        public static ProjectPlanModel Upgrade(
            IMapper mapper,
            v0_2_1.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(projectPlan);
            var compilationErrors = new List<GraphCompilationErrorModel>();

            if (projectPlan.GraphCompilation?.Errors != null)
            {

                // C0010
                if (projectPlan.GraphCompilation.Errors.MissingDependencies.Count != 0)
                {
                    compilationErrors.Add(
                        new GraphCompilationErrorModel
                        {
                            ErrorCode = GraphCompilationErrorCode.C0010,
                            ErrorMessage = BuildMissingDependenciesErrorMessage(
                                projectPlan.GraphCompilation.Errors.MissingDependencies,
                                projectPlan.GraphCompilation.DependentActivities),
                        });
                }

                // C0020
                if (projectPlan.GraphCompilation.Errors.CircularDependencies.Count != 0)
                {
                    compilationErrors.Add(
                        new GraphCompilationErrorModel
                        {
                            ErrorCode = GraphCompilationErrorCode.C0020,
                            ErrorMessage = BuildCircularDependenciesErrorMessage(projectPlan.GraphCompilation.Errors.CircularDependencies),
                        });
                }

                // C0030 - invalid constraints not recorded in v0.2.1

                // C0040
                if (projectPlan.GraphCompilation.Errors.AllResourcesExplicitTargetsButNotAllActivitiesTargeted)
                {
                    compilationErrors.Add(
                        new GraphCompilationErrorModel
                        {
                            ErrorCode = GraphCompilationErrorCode.C0040,
                            ErrorMessage = @"All resources are explicit targets, but not all activities have targeted resources",
                        });
                }
            }

            return new ProjectPlanModel
            {
                ProjectStart = projectPlan.ProjectStart,
                DependentActivities = mapper.Map<List<v0_2_1.DependentActivityModel>, List<DependentActivityModel>>(projectPlan.DependentActivities),
                ArrowGraphSettings = projectPlan.ArrowGraphSettings ?? new v0_1_0.ArrowGraphSettingsModel(),
                ResourceSettings = projectPlan.ResourceSettings ?? new v0_1_0.ResourceSettingsModel(),
                GraphCompilation = new GraphCompilationModel
                {
                    DependentActivities = mapper.Map<List<v0_2_1.DependentActivityModel>, List<DependentActivityModel>>(projectPlan.GraphCompilation?.DependentActivities ?? []),
                    ResourceSchedules = mapper.Map<List<v0_2_1.ResourceScheduleModel>, List<ResourceScheduleModel>>(projectPlan.GraphCompilation?.ResourceSchedules ?? []),
                    CompilationErrors = compilationErrors,
                    CyclomaticComplexity = projectPlan.GraphCompilation?.CyclomaticComplexity ?? default,
                    Duration = projectPlan.GraphCompilation?.Duration ?? default,
                },
                ArrowGraph = mapper.Map<v0_2_1.ArrowGraphModel, ArrowGraphModel>(projectPlan.ArrowGraph ?? new v0_2_1.ArrowGraphModel()),
                HasStaleOutputs = projectPlan.HasStaleOutputs,
            };
        }

        private static string BuildMissingDependenciesErrorMessage(
            IEnumerable<int> missingDependencies,
            IEnumerable<v0_2_1.DependentActivityModel> activityModels)
        {
            ArgumentNullException.ThrowIfNull(missingDependencies);
            ArgumentNullException.ThrowIfNull(activityModels);
            var output = new StringBuilder();
            output.AppendLine(@"Missing activity dependencies:");
            foreach (int missingDependency in missingDependencies)
            {
                IList<int> activities = activityModels
                    .Where(x => x.Dependencies.Contains(missingDependency))
                    .Select(x => x.Activity?.Id ?? default)
                    .Where(x => x != default)
                    .ToList();
                output.AppendFormat(CultureInfo.InvariantCulture, $@"{missingDependency} -> ");
                output.AppendLine(string.Join(@", ", activities));
            }
            return output.ToString();
        }

        private static string BuildCircularDependenciesErrorMessage(IEnumerable<v0_1_0.CircularDependencyModel> circularDependencies)
        {
            ArgumentNullException.ThrowIfNull(circularDependencies);
            var output = new StringBuilder();
            output.AppendLine(@"Circular activity dependencies:");
            foreach (v0_1_0.CircularDependencyModel circularDependency in circularDependencies)
            {
                output.AppendLine(string.Join(@" -> ", circularDependency.Dependencies));
            }
            return output.ToString();
        }
    }
}
