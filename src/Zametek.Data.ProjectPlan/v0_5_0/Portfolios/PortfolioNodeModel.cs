namespace Zametek.Data.ProjectPlan.v0_5_0
{
    [Serializable]
    public class PortfolioNodeModel
    {
        public Guid Id { get; init; }

        public Guid ParentId { get; init; }

        public string Comment { get; init; } = string.Empty;
    }
}