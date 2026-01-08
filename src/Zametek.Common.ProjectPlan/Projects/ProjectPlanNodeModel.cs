namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ProjectPlanNodeModel
    {
        public Guid Id { get; init; }

        public Guid ParentId { get; init; }

        public string Comment { get; init; } = string.Empty;

        public ProjectPlanModel ProjectPlan { get; init; } = new ProjectPlanModel();
    }
}