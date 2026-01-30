namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectPlanNodeModel
    {
        public Guid Id { get; init; }

        public Guid ParentId { get; init; }

        public ProjectPlanNodeType NodeType { get; init; }

        public string Name { get; init; } = string.Empty;

        public DateTimeOffset CreatedOn { get; init; }

        public DateTimeOffset ModifiedOn { get; init; }
    }
}