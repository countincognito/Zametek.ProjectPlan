namespace Zametek.Data.ProjectPlan.v0_4_1
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            VersionMapper mapper,
            v0_4_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            var displaySettings = project.DisplaySettings ?? new();

            var output = new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                Today = project.ProjectStart,
                DependentActivities = project.DependentActivities ?? [],
                ArrowGraphSettings = project.ArrowGraphSettings ?? new(),
                ResourceSettings = project.ResourceSettings ?? new(),
                WorkStreamSettings = project.WorkStreamSettings ?? new(),
                DisplaySettings = new()
                {
                    ShowDates = default,
                    UseClassicDates = default,
                    UseBusinessDays = default,
                    ArrowGraphShowNames = default,
                    GanttChartGroupByMode = displaySettings.GanttChartGroupByMode,
                    GanttChartAnnotationStyle = displaySettings.GanttChartAnnotationStyle,
                    GanttChartShowGroupLabels = displaySettings.ViewGanttChartGroupLabels,
                    GanttChartShowProjectFinish = displaySettings.ViewGanttChartProjectFinish,
                    GanttChartShowTracking = displaySettings.ViewGanttChartTracking,
                    GanttChartShowToday = default,
                    ResourceChartAllocationMode = default,
                    ResourceChartScheduleMode = default,
                    ResourceChartDisplayStyle = default,
                    ResourceChartShowToday = default,
                    EarnedValueShowProjections = displaySettings.ViewEarnedValueProjections,
                    EarnedValueShowToday = default
                },
                GraphCompilation = project.GraphCompilation ?? new(),
                ArrowGraph = project.ArrowGraph ?? new(),
                HasStaleOutputs = project.HasStaleOutputs,
            };

            return output;
        }

        public static AppSettingsModel Upgrade(v0_3_0.AppSettingsModel appSettingsModel)
        {
            return new AppSettingsModel
            {
                ProjectPlanDirectory = appSettingsModel.ProjectPlanDirectory,
                DefaultShowDates = appSettingsModel.ShowDates,
                DefaultUseClassicDates = appSettingsModel.UseClassicDates,
                DefaultUseBusinessDays = appSettingsModel.UseBusinessDays,
                SelectedTheme = appSettingsModel.SelectedTheme,
            };
        }
    }
}
