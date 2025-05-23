﻿using AutoMapper;

namespace Zametek.Data.ProjectPlan.v0_4_1
{
    public static class Converter
    {
        public static ProjectPlanModel Upgrade(
            IMapper mapper,
            v0_4_0.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(projectPlan);

            var displaySettings = projectPlan.DisplaySettings ?? new();

            var plan = new ProjectPlanModel
            {
                ProjectStart = projectPlan.ProjectStart,
                Today = projectPlan.ProjectStart,
                DependentActivities = projectPlan.DependentActivities ?? [],
                ArrowGraphSettings = projectPlan.ArrowGraphSettings ?? new(),
                ResourceSettings = projectPlan.ResourceSettings ?? new(),
                WorkStreamSettings = projectPlan.WorkStreamSettings ?? new(),
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
                GraphCompilation = projectPlan.GraphCompilation ?? new(),
                ArrowGraph = projectPlan.ArrowGraph ?? new(),
                HasStaleOutputs = projectPlan.HasStaleOutputs,
            };

            return plan;
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
