namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public class ProjectNodeModel
    {
        public Guid Id { get; init; }

        public Guid ParentId { get; init; }

        public string Comment { get; init; } = string.Empty;

        public ProjectPlanModel ProjectPlan { get; init; } = new ProjectPlanModel();
    }
}