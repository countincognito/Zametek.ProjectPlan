namespace Zametek.Data.ProjectPlan.v0_6_2
{
    // The resource settings of v0.6.1, with the resources of v0.6.2.
    [Serializable]
    public record ResourceSettingsModel
    {
        public List<ResourceModel> Resources { get; init; } = [];

        public double DefaultUnitCost { get; init; }

        public double DefaultUnitBilling { get; init; }

        public bool AreDisabled { get; init; }
    }
}
