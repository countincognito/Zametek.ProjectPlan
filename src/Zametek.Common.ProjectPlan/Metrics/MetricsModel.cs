namespace Zametek.Common.ProjectPlan
{
    public record MetricsModel
    {
        public RisksModel Risks { get; init; } = new();

        public CostsModel Costs { get; init; } = new();

        public BillingsModel Billings { get; init; } = new();

        public MarginsModel Margins { get; init; } = new();

        public EffortsModel Efforts { get; init; } = new();

        public NetworkModel Network { get; init; } = new();
    }
}
