namespace Zametek.Data.ProjectPlan.v0_6_1
{
    [Serializable]
    public record ProjectModel
    {
        public string Version { get; } = Versions.v0_6_1;

        public Guid Id { get; init; }

        public Guid Root { get; init; }

        public Guid Current { get; init; }

        public List<v0_6_0.ProjectScenarioNodeModel> Nodes { get; init; } = [];

        public List<ProjectScenarioFileModel> Files { get; init; } = [];

        public List<v0_6_0.ProjectScenarioTagModel> Tags { get; init; } = [];

        public v0_6_0.ProjectDisplaySettingsModel DisplaySettings { get; init; } = new();
    }
}
