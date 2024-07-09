namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record ResourceSettingsModel
    {
        public List<ResourceModel> Resources { get; init; } = [];

        public double DefaultUnitCost { get; init; }

        public bool AreDisabled { get; init; }
    }
}
