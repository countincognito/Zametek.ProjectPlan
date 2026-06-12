using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.v0_5_0
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            VersionMapper mapper,
            v0_4_4.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            List<v0_3_0.ActivitySeverityModel> activitySeverities = [];

            foreach (v0_1_0.ActivitySeverityModel activitySeverityModel in project.ArrowGraphSettings.ActivitySeverities)
            {
                activitySeverities.Add(new v0_3_0.ActivitySeverityModel
                {
                    SlackLimit = activitySeverityModel.SlackLimit,
                    CriticalityWeight = activitySeverityModel.CriticalityWeight,
                    FibonacciWeight = activitySeverityModel.FibonacciWeight,
                    ColorFormat = activitySeverityModel.ColorFormat ?? new(),
                });
            }

            List<NodeTypeFormatModel> nodeTypeFormats = [.. DefaultFormatCollections.NodeTypeFormats.Select(mapper.FromCurrentToV0_5_0)];

            GraphSettingsModel graphSettings = new()
            {
                NodeTypeFormats = nodeTypeFormats,
                EdgeTypeFormats = project.ArrowGraphSettings.EdgeTypeFormats,
                ActivitySeverities = activitySeverities,
            };

            DisplaySettingsModel displaySettings = mapper.FromV0_4_4ToV0_5_0(project.DisplaySettings ?? new());

            return new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                Today = project.Today,
                DependentActivities = project.DependentActivities,
                GraphSettings = graphSettings,
                ResourceSettings = project.ResourceSettings,
                WorkStreamSettings = project.WorkStreamSettings ?? new(),
                DisplaySettings = displaySettings,
            };
        }
    }
}
