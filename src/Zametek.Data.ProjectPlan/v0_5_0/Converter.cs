using AutoMapper;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.v0_5_0
{
    public static class Converter
    {
        public static ProjectPlanModel Upgrade(
            IMapper mapper,
            v0_4_4.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(projectPlan);

            List<v0_3_0.ActivitySeverityModel> activitySeverities = [];

            foreach (v0_1_0.ActivitySeverityModel activitySeverityModel in projectPlan.ArrowGraphSettings.ActivitySeverities)
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
                EdgeTypeFormats = projectPlan.ArrowGraphSettings.EdgeTypeFormats,
                ActivitySeverities = activitySeverities,
            };

            DisplaySettingsModel displaySettings = mapper.Map<v0_4_4.DisplaySettingsModel, DisplaySettingsModel>(projectPlan.DisplaySettings ?? new());

            var plan = new ProjectPlanModel
            {
                ProjectStart = projectPlan.ProjectStart,
                Today = projectPlan.ProjectStart,
                DependentActivities = projectPlan.DependentActivities,
                GraphSettings = graphSettings,
                ResourceSettings = projectPlan.ResourceSettings,
                WorkStreamSettings = projectPlan.WorkStreamSettings ?? new(),
                DisplaySettings = displaySettings,
            };

            return plan;
        }
    }
}
