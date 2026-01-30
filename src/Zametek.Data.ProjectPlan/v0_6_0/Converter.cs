using AutoMapper;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.v0_6_0
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            IMapper mapper,
            DateTimeOffset localNow,
            v0_5_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            Guid projectId = Guid.NewGuid();
            Guid rootId = Guid.NewGuid();
            Guid planId = Guid.NewGuid();

            return new ProjectModel
            {
                Id = projectId,
                Root = rootId,
                Current = planId,
                Nodes =
                [
                    new ProjectPlanNodeModel
                    {
                        Id = planId,
                        ParentId = rootId,
                        NodeType = ProjectPlanNodeType.File,
                        Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                        CreatedOn = localNow,
                        ModifiedOn = localNow,
                    },
                ],
                Files =
                [
                    new ProjectPlanFileModel
                    {
                        NodeId = planId,
                        Plan = mapper.Map<v0_5_0.ProjectModel, ProjectPlanModel>(project),
                    },
                ],
                Tags =
                [
                    new ProjectPlanTagModel
                    {
                        NodeId = rootId,
                        Label = Resource.ProjectPlan.Labels.Label_RootNode,
                    },
                    new ProjectPlanTagModel
                    {
                        NodeId = planId,
                        Label = Resource.ProjectPlan.Messages.Message_ConvertedFromPreviousVersion,
                    },
                ],
            };
        }

        public static AppSettingsModel Upgrade(
            IMapper mapper,
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
                ProjectPlanSortMode = default,
                ProjectPlanSortDirection = default,
                SelectedTheme = appSettingsModel.SelectedTheme,
            };
        }
    }
}
