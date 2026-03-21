using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.v0_6_0
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            VersionMapper mapper,
            DateTimeOffset localNow,
            v0_5_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            Guid projectId = Guid.NewGuid();
            Guid rootId = Guid.NewGuid();
            Guid scenarioId = Guid.NewGuid();

            return new ProjectModel
            {
                Id = projectId,
                Root = rootId,
                Current = scenarioId,
                Nodes =
                [
                    new ProjectScenarioNodeModel
                    {
                        Id = scenarioId,
                        ParentId = rootId,
                        NodeType = ProjectScenarioNodeType.File,
                        Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                        CreatedOn = localNow,
                        ModifiedOn = localNow,
                        IsTracked = false,
                    },
                ],
                Files =
                [
                    new ProjectScenarioFileModel
                    {
                        NodeId = scenarioId,
                        Scenario = mapper.FromV0_5_0ToV0_6_0(project),
                    },
                ],
                Tags =
                [
                    new ProjectScenarioTagModel
                    {
                        NodeId = rootId,
                        Label = Resource.ProjectPlan.Labels.Label_RootNode,
                    },
                    new ProjectScenarioTagModel
                    {
                        NodeId = scenarioId,
                        Label = Resource.ProjectPlan.Messages.Message_ConvertedFromPreviousVersion,
                    },
                ],
            };
        }

        public static AppSettingsModel Upgrade(
            VersionMapper mapper,
            v0_4_4.AppSettingsModel appSettingsModel)
        {
            return new AppSettingsModel
            {
                ProjectDirectory = appSettingsModel.ProjectPlanDirectory,
                DefaultShowDates = appSettingsModel.DefaultShowDates,
                DefaultUseClassicDates = appSettingsModel.DefaultUseClassicDates,
                DefaultUseBusinessDays = appSettingsModel.DefaultUseBusinessDays,
                DefaultHideCost = appSettingsModel.DefaultHideCost,
                DefaultHideBilling = appSettingsModel.DefaultHideBilling,
                ProjectScenarioSortMode = default,
                ProjectScenarioSortDirection = default,
                ScenarioChartTrackedMetricXAxis = default,
                ScenarioChartTrackedMetricYAxis = default,
                ScenarioChartCurveFittingType = default,
                SelectedTheme = appSettingsModel.SelectedTheme,
            };
        }
    }
}
