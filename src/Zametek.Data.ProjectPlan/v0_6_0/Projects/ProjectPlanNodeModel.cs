namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public record ProjectPlanNodeModel
    {
        public Guid Id { get; init; }

        public Guid ParentId { get; init; }

        public bool IsFolder { get; init; }

        public string Name { get; init; } = string.Empty;

        public DateTimeOffset CreatedOn { get; init; } = DateTimeOffset.UtcNow;

        public DateTimeOffset ModifiedOn { get; init; } = DateTimeOffset.UtcNow;
    }
}