namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record PortfolioModel
    {
        public string Version { get; init; } = string.Empty;

        public Guid Root { get; init; }

        public List<PortfolioNodeModel> Nodes { get; init; } = [];

        public List<PortfolioBranchModel> Branches { get; init; } = [];
    }
}
