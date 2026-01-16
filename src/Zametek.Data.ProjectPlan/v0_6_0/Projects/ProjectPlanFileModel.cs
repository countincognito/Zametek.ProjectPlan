namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public record ProjectPlanFileModel
    {
        public Guid NodeId { get; init; }

        public ProjectPlanModel Plan { get; init; } = new();
    }
}