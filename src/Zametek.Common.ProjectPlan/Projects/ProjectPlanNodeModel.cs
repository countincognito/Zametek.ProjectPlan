namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectPlanNodeModel
    {
        public Guid Id { get; init; }

        public Guid ParentId { get; init; }

        public DateTimeOffset CreatedOn { get; init; } = DateTimeOffset.UtcNow;

        public DateTimeOffset ModifiedOn { get; init; } = DateTimeOffset.UtcNow;

        public string Comment { get; init; } = string.Empty;

        public ProjectPlanModel ProjectPlan { get; init; } = new ProjectPlanModel();
    }
}