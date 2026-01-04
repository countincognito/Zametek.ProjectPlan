namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class PortfolioBranchModel
    {
        public Guid NodeId { get; init; }

        public string Label { get; init; } = string.Empty;
    }
}