namespace Zametek.Data.ProjectPlan.v0_5_0
{
    [Serializable]
    public class PortfolioBranchModel
    {
        public Guid NodeId { get; init; }

        public string Label { get; init; } = string.Empty;
    }
}