namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectModel
    {
        public string Version { get; init; } = string.Empty;

        public Guid Id { get; init; }

        public Guid Root { get; init; }

        public Guid Current { get; init; }

        public List<ProjectScenarioNodeModel> Nodes { get; init; } = [];

        public List<ProjectScenarioFileModel> Files { get; init; } = [];

        public List<ProjectScenarioTagModel> Tags { get; init; } = [];

        public ProjectDisplaySettingsModel DisplaySettings { get; init; } = new();
    }
}
