using AutoMapper;

namespace Zametek.Data.ProjectPlan.v0_6_0
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            IMapper mapper,
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
                        IsFolder = false,
                        Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                        CreatedOn = DateTimeOffset.UtcNow,
                        ModifiedOn = DateTimeOffset.UtcNow,
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
    }
}
