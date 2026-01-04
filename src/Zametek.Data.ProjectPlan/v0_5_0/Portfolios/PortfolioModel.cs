namespace Zametek.Data.ProjectPlan.v0_5_0
{
    [Serializable]
    public record PortfolioModel
    {
        public string Version { get; } = Versions.v0_5_0;

        public Guid Root { get; init; }

        public List<PortfolioNodeModel> Nodes { get; init; } = [];

        public List<PortfolioBranchModel> Branches { get; init; } = [];
    }
}
