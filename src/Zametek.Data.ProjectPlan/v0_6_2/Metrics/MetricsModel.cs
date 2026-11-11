namespace Zametek.Data.ProjectPlan.v0_6_2
{
    // The metrics of a scenario: those of v0.5.0, and the headcount, which is new in v0.6.2.
    [Serializable]
    public record MetricsModel
    {
        public v0_5_0.RisksModel Risks { get; init; } = new();

        public v0_5_0.CostsModel Costs { get; init; } = new();

        public v0_5_0.BillingsModel Billings { get; init; } = new();

        public v0_5_0.MarginsModel Margins { get; init; } = new();

        public v0_5_0.EffortsModel Efforts { get; init; } = new();

        public v0_5_0.NetworkModel Network { get; init; } = new();

        public HeadcountModel Headcount { get; init; } = new();
    }
}
