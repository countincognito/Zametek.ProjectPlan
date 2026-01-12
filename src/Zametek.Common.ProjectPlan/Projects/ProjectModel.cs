namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectModel
    {
        public string Version { get; init; } = string.Empty;

        public Guid Root { get; init; }

        public Guid Current { get; init; }

        public List<ProjectPlanNodeModel> Nodes { get; init; } = [];

        public List<ProjectPlanTagModel> Tags { get; init; } = [];
    }
}
