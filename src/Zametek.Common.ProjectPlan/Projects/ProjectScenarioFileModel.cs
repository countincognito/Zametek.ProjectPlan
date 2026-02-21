namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectScenarioFileModel
    {
        public Guid NodeId { get; init; }

        public ProjectScenarioModel Scenario { get; init; } = new();
    }
}