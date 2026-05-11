using System.Globalization;
using System.Text;

namespace Zametek.Data.ProjectPlan.v0_3_0
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            VersionMapper mapper,
            v0_2_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);
            var compilationErrors = new List<GraphCompilationErrorModel>();

            if (project.GraphCompilation?.Errors != null)
            {

                // C0010
                if (project.GraphCompilation.Errors.MissingDependencies.Count != 0)
                {
                    compilationErrors.Add(
                        new GraphCompilationErrorModel
                        {
                            ErrorCode = GraphCompilationErrorCode.C0010,
                            ErrorMessage = BuildMissingDependenciesErrorMessage(
                                project.GraphCompilation.Errors.MissingDependencies,
                                project.GraphCompilation.DependentActivities),
                        });
                }

                // C0020
                if (project.GraphCompilation.Errors.CircularDependencies.Count != 0)
                {
                    compilationErrors.Add(
                        new GraphCompilationErrorModel
                        {
                            ErrorCode = GraphCompilationErrorCode.C0020,
                            ErrorMessage = BuildCircularDependenciesErrorMessage(project.GraphCompilation.Errors.CircularDependencies),
                        });
                }

                // C0030 - invalid constraints not recorded in v0.2.1

                // C0040
                if (project.GraphCompilation.Errors.AllResourcesExplicitTargetsButNotAllActivitiesTargeted)
                {
                    compilationErrors.Add(
                        new GraphCompilationErrorModel
                        {
                            ErrorCode = GraphCompilationErrorCode.C0040,
                            ErrorMessage = @"All resources are explicit targets, but not all activities have targeted resources",
                        });
                }
            }

            return new ProjectModel
            {
                // v0_2_1 stored ProjectStart as DateTime (no offset). Treat it as UTC when
                // promoting to DateTimeOffset so the conversion is timezone-independent.
                ProjectStart = new DateTimeOffset(project.ProjectStart, TimeSpan.Zero),
                DependentActivities = [.. project.DependentActivities.Select(mapper.FromV0_2_1ToV0_3_0)],
                ArrowGraphSettings = project.ArrowGraphSettings ?? new v0_1_0.ArrowGraphSettingsModel(),
                ResourceSettings = project.ResourceSettings ?? new v0_1_0.ResourceSettingsModel(),
                GraphCompilation = new GraphCompilationModel
                {
                    DependentActivities = [.. (project.GraphCompilation?.DependentActivities ?? []).Select(mapper.FromV0_2_1ToV0_3_0)],
                    ResourceSchedules = [.. (project.GraphCompilation?.ResourceSchedules ?? []).Select(mapper.FromV0_2_1ToV0_3_0)],
                    CompilationErrors = compilationErrors,
                    CyclomaticComplexity = project.GraphCompilation?.CyclomaticComplexity ?? default,
                    Duration = project.GraphCompilation?.Duration ?? default,
                },
                ArrowGraph = mapper.FromV0_2_1ToV0_3_0(project.ArrowGraph ?? new v0_2_1.ArrowGraphModel()),
                HasStaleOutputs = project.HasStaleOutputs,
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
