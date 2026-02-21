namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectScenarioTagModel
    {
        public Guid NodeId { get; init; }

        public string Label { get; init; } = string.Empty;
    }
}