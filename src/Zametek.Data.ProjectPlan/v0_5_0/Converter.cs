using AutoMapper;

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

            DisplaySettingsModel displaySettings = mapper.Map<v0_4_4.DisplaySettingsModel, DisplaySettingsModel>(projectPlan.DisplaySettings ?? new());

            var plan = new ProjectPlanModel
            {
                ProjectStart = projectPlan.ProjectStart,
                Today = projectPlan.ProjectStart,
                DependentActivities = projectPlan.DependentActivities,
                ArrowGraphSettings = projectPlan.ArrowGraphSettings ?? new(),
                ResourceSettings = projectPlan.ResourceSettings,
                WorkStreamSettings = projectPlan.WorkStreamSettings ?? new(),
                DisplaySettings = displaySettings,
            };

            return plan;
        }
    }
}
