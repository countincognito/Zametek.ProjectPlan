namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public record ProjectModel
    {
        public string Version { get; } = Versions.v0_6_0;

        public Guid Root { get; init; }

        public List<ProjectPlanNodeModel> Nodes { get; init; } = [];

        public List<ProjectPlanBranchModel> Branches { get; init; } = [];
    }
}
