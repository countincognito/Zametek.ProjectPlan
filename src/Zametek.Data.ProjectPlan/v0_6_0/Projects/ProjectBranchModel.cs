namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public class ProjectBranchModel
    {
        public Guid NodeId { get; init; }

        public string Label { get; init; } = string.Empty;
    }
}