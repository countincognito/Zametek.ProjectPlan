namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public record ProjectScenarioFileModel
    {
        public Guid NodeId { get; init; }

        public ProjectScenarioModel Scenario { get; init; } = new();
    }
}