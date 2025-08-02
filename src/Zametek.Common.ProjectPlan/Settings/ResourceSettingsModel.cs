namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ResourceSettingsModel
    {
        public List<ResourceModel> Resources { get; init; } = [];

        public double DefaultUnitCost { get; init; }

        public double DefaultUnitBilling { get; init; }

        public bool AreDisabled { get; init; }
    }
}
