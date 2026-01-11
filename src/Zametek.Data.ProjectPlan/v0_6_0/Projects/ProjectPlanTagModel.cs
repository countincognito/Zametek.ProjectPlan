namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public record ProjectPlanTagModel
    {
        public Guid NodeId { get; init; }

        public string Label { get; init; } = string.Empty;
    }
}