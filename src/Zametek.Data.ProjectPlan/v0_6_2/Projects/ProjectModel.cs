namespace Zametek.Data.ProjectPlan.v0_6_2
{
    [Serializable]
    public record ProjectModel
    {
        public string Version { get; } = Versions.v0_6_2;

        public Guid Id { get; init; }

        public Guid Root { get; init; }

        public Guid Current { get; init; }

        public List<v0_6_0.ProjectScenarioNodeModel> Nodes { get; init; } = [];

        public List<ProjectScenarioFileModel> Files { get; init; } = [];

        public List<v0_6_0.ProjectScenarioTagModel> Tags { get; init; } = [];

        // v0.6.2 changes only the activities, so the project display settings are those of v0.6.1.
        public v0_6_1.ProjectDisplaySettingsModel DisplaySettings { get; init; } = new();
    }
}
