namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record FileSettingsModel
    {
        public string Version { get; } = Versions.v0_3_0;

        public string ProjectPlanDirectory { get; init; } = string.Empty;
    }
}
