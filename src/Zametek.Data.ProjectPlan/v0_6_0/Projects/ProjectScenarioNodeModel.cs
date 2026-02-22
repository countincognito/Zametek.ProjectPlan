using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public record ProjectScenarioNodeModel
    {
        public Guid Id { get; init; }

        public Guid ParentId { get; init; }

        public ProjectScenarioNodeType NodeType { get; init; }

        public string Name { get; init; } = string.Empty;

        public DateTimeOffset CreatedOn { get; init; }

        public bool IsTracked { get; init; }

        public DateTimeOffset ModifiedOn { get; init; }
    }
}