namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ProjectPlanBranchModel
    {
        public Guid NodeId { get; init; }

        public string Label { get; init; } = string.Empty;
    }
}