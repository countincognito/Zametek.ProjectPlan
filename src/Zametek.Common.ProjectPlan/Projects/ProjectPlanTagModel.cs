namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectPlanTagModel
    {
        public Guid NodeId { get; init; }

        public string Label { get; init; } = string.Empty;
    }
}