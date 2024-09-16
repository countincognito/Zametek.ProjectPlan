namespace Zametek.Data.ProjectPlan.v0_4_0
{
    [Serializable]
    public record ResourceSettingsModel
    {
        public List<v0_3_2.ResourceModel> Resources { get; init; } = [];

        public double DefaultUnitCost { get; init; }

        public bool AreDisabled { get; init; }
    }
}
