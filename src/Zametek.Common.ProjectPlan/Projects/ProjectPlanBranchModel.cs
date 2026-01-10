namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectPlanBranchModel
    {
        public Guid NodeId { get; init; }

        public string Label { get; init; } = string.Empty;
    }
}