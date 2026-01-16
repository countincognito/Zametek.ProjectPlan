namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectPlanFileModel
    {
        public Guid NodeId { get; init; }

        public ProjectPlanModel Plan { get; init; } = new();
    }
}