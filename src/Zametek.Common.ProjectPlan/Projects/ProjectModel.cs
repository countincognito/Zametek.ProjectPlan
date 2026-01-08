namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectModel
    {
        public string Version { get; init; } = string.Empty;

        public Guid Root { get; init; }

        public List<ProjectPlanNodeModel> PlanNodes { get; init; } = [];

        public List<ProjectBranchModel> Branches { get; init; } = [];
    }
}
