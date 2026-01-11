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

            Guid rootId = Guid.NewGuid();
            Guid planId = Guid.NewGuid();

            return new ProjectModel
            {
                Root = rootId,
                Nodes =
                [
                    new ProjectPlanNodeModel
                    {
                        Id = planId,
                        ParentId = rootId,
                        Comment = "Converted from v0.5.0",
                        ProjectPlan = mapper.Map<v0_5_0.ProjectModel, ProjectPlanModel>(project),
                    },
                ],
                Tags =
                [
                    new ProjectPlanTagModel
                    {
                        NodeId = rootId,
                        Label = Resource.ProjectPlan.Labels.Label_RootNode,
                    },
                ],
            };
        }
    }
}
