namespace Zametek.Data.ProjectPlan.v0_6_1
{
    [Serializable]
    public record ResourceMetricsModel
    {
        // Null when the resource series has no settings resource behind it
        // (an implicit/spare resource created during scheduling).
        public int? ResourceId { get; init; }

        public string ResourceName { get; init; } = string.Empty;

        public ResourceCostsModel Costs { get; init; } = new();

        public ResourceBillingsModel Billings { get; init; } = new();

        public ResourceMarginsModel Margins { get; init; } = new();

        public ResourceEffortsModel Efforts { get; init; } = new();
    }
}
