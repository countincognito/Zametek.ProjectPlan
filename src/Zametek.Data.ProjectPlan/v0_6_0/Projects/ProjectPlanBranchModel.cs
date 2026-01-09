namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public class ProjectPlanBranchModel
    {
        public Guid NodeId { get; init; }

        public string Label { get; init; } = string.Empty;
    }
}