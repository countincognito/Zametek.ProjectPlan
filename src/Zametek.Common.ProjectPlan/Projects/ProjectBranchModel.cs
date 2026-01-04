namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ProjectBranchModel
    {
        public Guid NodeId { get; init; }

        public string Label { get; init; } = string.Empty;
    }
}