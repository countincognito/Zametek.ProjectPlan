using AutoMapper;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.v0_5_0
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            IMapper mapper,
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

            var nodeTypeFormats = mapper.Map<List<Common.ProjectPlan.NodeTypeFormatModel>, List<NodeTypeFormatModel>>(DefaultFormatCollections.NodeTypeFormats);

            GraphSettingsModel graphSettings = new()
            {
                NodeTypeFormats = nodeTypeFormats,
                EdgeTypeFormats = project.ArrowGraphSettings.EdgeTypeFormats,
                ActivitySeverities = activitySeverities,
            };

            DisplaySettingsModel displaySettings = mapper.Map<v0_4_4.DisplaySettingsModel, DisplaySettingsModel>(project.DisplaySettings ?? new());

            return new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                Today = project.ProjectStart,
                DependentActivities = project.DependentActivities,
                GraphSettings = graphSettings,
                ResourceSettings = project.ResourceSettings,
                WorkStreamSettings = project.WorkStreamSettings ?? new(),
                DisplaySettings = displaySettings,
            };
        }
    }
}
